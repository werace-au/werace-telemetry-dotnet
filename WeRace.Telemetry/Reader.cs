using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Text;

namespace WeRace.Telemetry;

/// <summary>
/// Handles reading telemetry data from a stream.
/// </summary>
/// <typeparam name="SESSION">The session type with a fixed size structure.</typeparam>
/// <typeparam name="FRAME">The frame type with a fixed size structure.</typeparam>
public sealed class Reader<SESSION, FRAME> where SESSION : struct where FRAME : struct
{
  private const int FIXED_HEADER_SIZE = 40;
  private const int FOOTER_SIZE = 24; // Magic(8) + FrameCount(8) + LastTick(8)
  private const int METADATA_COUNT_OFFSET = 32;
  private const int METADATA_START_OFFSET = 40;
  private const int LENGTH_FIELD_SIZE = 4;
  private const int SESSION_ENTRY_SIZE = 24;

  private readonly Stream _stream;
  private readonly int _frameSize;
  private readonly int _headerSize;
  private readonly int _totalFrameSize;
  private readonly int _sessionSize;
  private readonly int _sessionHeaderSize;

  public Header Header { get; }
  public IReadOnlyList<SessionInfo<SESSION>> Sessions { get; }

  private Reader(Stream stream, Header header, IReadOnlyList<SessionInfo<SESSION>> sessions)
  {
    _stream = stream;
    Header = header;
    Sessions = sessions;
    _frameSize = TelemetrySizeHelper<SESSION, FRAME>.FrameSize;
    _headerSize = TelemetrySizeHelper<SESSION, FRAME>.HeaderSize;
    _sessionSize = TelemetrySizeHelper<SESSION, FRAME>.SessionSize;
    _totalFrameSize = TelemetrySizeHelper<SESSION, FRAME>.TotalFrameSize;
    _sessionHeaderSize = TelemetrySizeHelper<SESSION, FRAME>.SessionHeaderSize;
  }

  /// <summary>
  /// Opens a telemetry data stream for reading.
  /// </summary>
  /// <param name="stream">The stream containing telemetry data. Must be readable and seekable.</param>
  /// <returns>A Reader instance for accessing session and frame data.</returns>
  /// <exception cref="ArgumentException">Thrown if the stream is not readable or seekable.</exception>
  public static Reader<SESSION, FRAME> Open(Stream stream)
  {
    ArgumentNullException.ThrowIfNull(stream);
    if (!stream.CanRead || !stream.CanSeek)
      throw new ArgumentException("Stream must be readable and seekable", nameof(stream));

    var header = ReadHeader(stream);
    var sessions = TryReadDocumentFooter(stream) ?? ReadSessionsFromStart(stream);

    return new Reader<SESSION, FRAME>(stream, header, sessions);
  }

  private static Header ReadHeader(Stream stream)
  {
    Span<byte> headerBuffer = stackalloc byte[FIXED_HEADER_SIZE];
    stream.ReadExactly(headerBuffer);

    if (!SpanReader.TryReadMagic(headerBuffer[..Magic.MAGIC_SIZE], Magic.FileMagic))
      throw new InvalidDataException("Invalid WRTF file magic number");

    var version = BinaryPrimitives.ReadUInt64LittleEndian(headerBuffer.Slice(8, 8));
    var sampleRate = BinaryPrimitives.ReadUInt64LittleEndian(headerBuffer.Slice(16, 8));
    var timestamp = BinaryPrimitives.ReadUInt64LittleEndian(headerBuffer.Slice(24, 8));
    var metadataCount = BinaryPrimitives.ReadUInt32LittleEndian(headerBuffer.Slice(32, 4));

    var metadata = ReadMetadataEntries(stream, METADATA_START_OFFSET, metadataCount);
    return new Header
    {
      Version = version,
      SampleRate = sampleRate,
      StartTimestamp = timestamp,
      Metadata = new Metadata(metadata)
    };
  }

  private static IReadOnlyDictionary<string, string> ReadMetadataEntries(Stream stream, long offset, uint count)
  {
    var entries = new Dictionary<string, string>((int)count);
    stream.Position = offset;

    // Pre-calculate total metadata size to allocate buffer once
    stream.Position = offset;
    var totalSize = 0;
    var positions = new (int keyStart, int keyLength, int valueStart, int valueLength)[count];
    var currentPos = 0;
    Span<byte> lengthBuffer = stackalloc byte[LENGTH_FIELD_SIZE];

    for (var i = 0; i < count; i++)
    {
      stream.ReadExactly(lengthBuffer);
      var keyLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
      var keyStart = currentPos;
      currentPos += keyLength;

      var padding = SpanReader.GetPadding(keyLength + LENGTH_FIELD_SIZE);
      stream.Seek(keyLength + padding, SeekOrigin.Current);

      stream.ReadExactly(lengthBuffer);
      var valueLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
      var valueStart = currentPos;
      currentPos += valueLength;

      padding = SpanReader.GetPadding(valueLength + LENGTH_FIELD_SIZE);
      stream.Seek(valueLength + padding, SeekOrigin.Current);

      positions[i] = (keyStart, keyLength, valueStart, valueLength);
      totalSize = Math.Max(totalSize, Math.Max(keyLength, valueLength));
    }

    // Now read the actual data using a single buffer
    var buffer = new byte[totalSize];
    stream.Position = offset;

    for (var i = 0; i < count; i++)
    {
      stream.ReadExactly(lengthBuffer); // Skip length, we already know it
      var (keyStart, keyLength, valueStart, valueLength) = positions[i];

      stream.ReadExactly(buffer.AsSpan(0, keyLength));
      var key = Encoding.UTF8.GetString(buffer, 0, keyLength);

      var padding = SpanReader.GetPadding(keyLength + LENGTH_FIELD_SIZE);
      stream.Seek(padding, SeekOrigin.Current);

      stream.ReadExactly(lengthBuffer); // Skip length
      stream.ReadExactly(buffer.AsSpan(0, valueLength));
      var value = Encoding.UTF8.GetString(buffer, 0, valueLength);

      padding = SpanReader.GetPadding(valueLength + LENGTH_FIELD_SIZE);
      stream.Seek(padding, SeekOrigin.Current);

      entries.Add(key, value);
    }

    return entries.ToFrozenDictionary();
  }

  private static List<SessionInfo<SESSION>>? TryReadDocumentFooter(Stream stream)
  {
    const int minFooterSize = Magic.MAGIC_SIZE * 2 + sizeof(ulong); // Start magic + End magic + Session count
    if (stream.Length < minFooterSize) return null;

    // Try to read document footer end marker
    stream.Position = stream.Length - Magic.MAGIC_SIZE;
    Span<byte> magic = stackalloc byte[Magic.MAGIC_SIZE];
    stream.ReadExactly(magic);
    if (!SpanReader.TryReadMagic(magic, Magic.DocumentFooterEndMagic)) return null;

    // Read number of sessions
    stream.Position = stream.Length - Magic.MAGIC_SIZE - sizeof(ulong);
    Span<byte> countBuffer = stackalloc byte[sizeof(ulong)];
    stream.ReadExactly(countBuffer);
    var sessionCount = BinaryPrimitives.ReadUInt64LittleEndian(countBuffer);

    // Verify document footer start marker
    var startMarkerPos = stream.Length - Magic.MAGIC_SIZE - sizeof(ulong) - (long)(sessionCount * (ulong)SESSION_ENTRY_SIZE) - Magic.MAGIC_SIZE;
    if (startMarkerPos < FIXED_HEADER_SIZE) return null;

    stream.Position = startMarkerPos;
    stream.ReadExactly(magic);
    if (!SpanReader.TryReadMagic(magic, Magic.DocumentFooterStartMagic)) return null;

    // Read session entries
    var sessions = new List<SessionInfo<SESSION>>((int)Math.Min(sessionCount, int.MaxValue));
    var buffer = new byte[SESSION_ENTRY_SIZE];

    for (var i = 0UL; i < sessionCount; i++)
    {
      stream.ReadExactly(buffer);
      var sessionOffset = BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(0, 8));
      var footerOffset = BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(8, 8));
      var frameCount = BinaryPrimitives.ReadUInt64LittleEndian(buffer.AsSpan(16, 8));

      // Verify session header
      stream.Position = sessionOffset;
      stream.ReadExactly(magic);
      if (!SpanReader.TryReadMagic(magic, Magic.SessionMagic)) continue;

      var session = ReadSessionData<SESSION>(stream, sessionOffset);
      var dataStart = sessionOffset + TelemetrySizeHelper<SESSION, FRAME>.SessionHeaderSize;

      sessions.Add(new SessionInfo<SESSION>
      {
        Data = session,
        FrameCount = frameCount,
        LastFrameTick = frameCount > 0 ? frameCount - 1 : 0,
        StartOffset = sessionOffset,
        DataOffset = dataStart
      });
    }

    return sessions;
  }

  private static List<SessionInfo<SESSION>> ReadSessionsFromStart(Stream stream)
  {
    var sessions = new List<SessionInfo<SESSION>>();
    var magic = new byte[Magic.MAGIC_SIZE];
    var minPos = FIXED_HEADER_SIZE + GetMetadataSize(stream);
    var currentPos = minPos;
    var totalFrameSize = TelemetrySizeHelper<SESSION, FRAME>.TotalFrameSize;
    var sessionHeaderSize = TelemetrySizeHelper<SESSION, FRAME>.SessionHeaderSize;

    while (currentPos < stream.Length)
    {
      stream.Position = currentPos;
      if (stream.Read(magic) != Magic.MAGIC_SIZE) break;

      if (SpanReader.TryReadMagic(magic, Magic.SessionMagic))
      {
        var session = ReadSessionData<SESSION>(stream, currentPos);
        var dataStart = currentPos + sessionHeaderSize;
        var frameCount = 0UL;
        var lastFrameTick = 0UL;

        // Scan forward to find next session or footer
        var nextPos = dataStart;
        while (nextPos + Magic.MAGIC_SIZE <= stream.Length)
        {
          stream.Position = nextPos;
          if (stream.Read(magic) != Magic.MAGIC_SIZE) break;

          if (SpanReader.TryReadMagic(magic, Magic.SessionMagic) ||
              SpanReader.TryReadMagic(magic, Magic.DocumentFooterStartMagic))
          {
            break;
          }

          if (SpanReader.TryReadMagic(magic, Magic.SessionFooterMagic))
          {
            var footerData = new byte[16];
            stream.ReadExactly(footerData);
            frameCount = BinaryPrimitives.ReadUInt64LittleEndian(footerData.AsSpan(0, 8));
            lastFrameTick = BinaryPrimitives.ReadUInt64LittleEndian(footerData.AsSpan(8, 8));
            nextPos += FOOTER_SIZE;
            break;
          }

          nextPos += totalFrameSize;
          frameCount++;
          lastFrameTick++;
        }

        sessions.Add(new SessionInfo<SESSION>
        {
          Data = session,
          FrameCount = frameCount,
          LastFrameTick = lastFrameTick,
          StartOffset = currentPos,
          DataOffset = dataStart
        });

        currentPos = nextPos;
      }
      else
      {
        currentPos += Magic.MAGIC_SIZE;
      }
    }

    return sessions;
  }

  private static int GetMetadataSize(Stream stream)
  {
    stream.Position = METADATA_COUNT_OFFSET;
    Span<byte> countBuffer = stackalloc byte[LENGTH_FIELD_SIZE];
    stream.ReadExactly(countBuffer);
    var count = BinaryPrimitives.ReadUInt32LittleEndian(countBuffer);

    var currentPos = METADATA_START_OFFSET;
    for (var i = 0; i < count; i++)
    {
      stream.Position = currentPos;
      stream.ReadExactly(countBuffer);
      var keyLength = BinaryPrimitives.ReadInt32LittleEndian(countBuffer);
      currentPos += LENGTH_FIELD_SIZE + keyLength +
                    SpanReader.GetPadding(currentPos + LENGTH_FIELD_SIZE + keyLength);

      stream.Position = currentPos;
      stream.ReadExactly(countBuffer);
      var valueLength = BinaryPrimitives.ReadInt32LittleEndian(countBuffer);
      currentPos += LENGTH_FIELD_SIZE + valueLength +
                    SpanReader.GetPadding(currentPos + LENGTH_FIELD_SIZE + valueLength);
    }

    return currentPos - METADATA_START_OFFSET;
  }

  private static T ReadSessionData<T>(Stream stream, long position) where T : struct
  {
    stream.Position = position + Magic.MAGIC_SIZE;
    var buffer = new byte[TelemetrySizeHelper<SESSION, FRAME>.SessionSize];
    stream.ReadExactly(buffer);
    return SpanReader.ReadStruct<T>(buffer);
  }

  /// <summary>
  /// Enumerates frames within a specified session.
  /// </summary>
  /// <param name="session">The session information from which to read frames.</param>
  /// <returns>An enumerable of frames.</returns>
  public IEnumerable<Frame<FRAME>> GetFrames(SessionInfo<SESSION> session)
  {
    if (session.FrameCount == 0) yield break;

    _stream.Position = session.DataOffset;
    var buffer = new byte[_totalFrameSize];

    for (var i = 0L; i < (long)session.FrameCount; i++)
    {
      _stream.ReadExactly(buffer);
      var header = SpanReader.ReadStruct<FrameHeader>(buffer.AsSpan(0, _headerSize));
      var frameData = SpanReader.ReadStruct<FRAME>(buffer.AsSpan(_headerSize, _frameSize));

      yield return new Frame<FRAME>
      {
        Header = header,
        Data = frameData
      };
    }
  }
}
