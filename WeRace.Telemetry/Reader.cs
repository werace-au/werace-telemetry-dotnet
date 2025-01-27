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
  private const int MAGIC_SIZE = 8;
  private const int FOOTER_SIZE = 24; // Magic(8) + FrameCount(8) + LastTick(8)
  private const int METADATA_COUNT_OFFSET = 32;
  private const int METADATA_START_OFFSET = 40;
  private const int LENGTH_FIELD_SIZE = 4;

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
    _frameSize = SpanReader.GetAlignedSize<FRAME>();
    _headerSize = SpanReader.GetAlignedSize<FrameHeader>();
    _sessionSize = SpanReader.GetAlignedSize<SESSION>();
    _totalFrameSize = _frameSize + _headerSize + SpanReader.GetPadding(_frameSize + _headerSize);
    _sessionHeaderSize = MAGIC_SIZE + _sessionSize + SpanReader.GetPadding(MAGIC_SIZE + _sessionSize);
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
    var sessions = ReadSessionsFromEnd(stream);

    return new Reader<SESSION, FRAME>(stream, header, sessions);
  }

  private static Header ReadHeader(Stream stream)
  {
    Span<byte> headerBuffer = stackalloc byte[FIXED_HEADER_SIZE];
    stream.ReadExactly(headerBuffer);

    if (!SpanReader.TryReadMagic(headerBuffer[..MAGIC_SIZE], Magic.FileMagic))
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
    var buffer = new byte[1024];
    Span<byte> lengthBuffer = stackalloc byte[LENGTH_FIELD_SIZE];

    for (var i = 0; i < count; i++)
    {
      stream.ReadExactly(lengthBuffer);
      var keyLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);

      if (keyLength > buffer.Length)
        Array.Resize(ref buffer, keyLength);

      stream.ReadExactly(buffer.AsSpan(0, keyLength));
      var key = Encoding.UTF8.GetString(buffer, 0, keyLength);

      var padding = SpanReader.GetPadding(keyLength + LENGTH_FIELD_SIZE);
      if (padding > 0)
        stream.Seek(padding, SeekOrigin.Current);

      stream.ReadExactly(lengthBuffer);
      var valueLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);

      if (valueLength > buffer.Length)
        Array.Resize(ref buffer, valueLength);

      stream.ReadExactly(buffer.AsSpan(0, valueLength));
      var value = Encoding.UTF8.GetString(buffer, 0, valueLength);

      padding = SpanReader.GetPadding(valueLength + LENGTH_FIELD_SIZE);
      if (padding > 0)
        stream.Seek(padding, SeekOrigin.Current);

      entries.Add(key, value);
    }

    return entries.ToFrozenDictionary();
  }

  private static List<SessionInfo<SESSION>> ReadSessionsFromEnd(Stream stream)
  {
    var sessions = new List<SessionInfo<SESSION>>();
    var magic = new byte[MAGIC_SIZE];
    var footerData = new byte[FOOTER_SIZE - MAGIC_SIZE];
    var minPos = FIXED_HEADER_SIZE + GetMetadataSize(stream);
    var currentPos = stream.Length;
    var defaultHeader = new Header
    {
      Version = 1,
      SampleRate = 1,
      StartTimestamp = 0,
      Metadata = new Metadata(new Dictionary<string, string>())
    };
    var reader = new Reader<SESSION, FRAME>(stream, defaultHeader, sessions);
    var totalFrameSize = reader._totalFrameSize;
    var sessionHeaderSize = reader._sessionHeaderSize;

    while (currentPos > minPos)
    {
      if (currentPos < FOOTER_SIZE + minPos) break;

      stream.Position = currentPos - FOOTER_SIZE;
      stream.ReadExactly(magic);

      if (SpanReader.TryReadMagic(magic, Magic.SessionFooterMagic))
      {
        stream.ReadExactly(footerData);
        var frameCount = BinaryPrimitives.ReadUInt64LittleEndian(footerData.AsSpan(0, 8));
        var lastFrameTick = BinaryPrimitives.ReadUInt64LittleEndian(footerData.AsSpan(8, 8));

        // Calculate session start based on frame count and sizes
        var dataSize = (long)frameCount * totalFrameSize;
        var sessionStart = currentPos - FOOTER_SIZE - dataSize - sessionHeaderSize;

        if (sessionStart >= minPos)
        {
          // Verify session header
          stream.Position = sessionStart;
          stream.ReadExactly(magic);
          if (SpanReader.TryReadMagic(magic, Magic.SessionMagic))
          {
            var session = ReadSessionData<SESSION>(stream, sessionStart);
            var dataStart = sessionStart + sessionHeaderSize;

            sessions.Insert(0, new SessionInfo<SESSION>
            {
              Data = session,
              FrameCount = frameCount,
              LastFrameTick = lastFrameTick,
              StartOffset = sessionStart,
              DataOffset = dataStart
            });

            currentPos = sessionStart;
          }
        }
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
    stream.Position = position + MAGIC_SIZE;
    var defaultHeader = new Header
    {
      Version = 1,
      SampleRate = 1,
      StartTimestamp = 0,
      Metadata = new Metadata(new Dictionary<string, string>())
    };
    var reader = new Reader<SESSION, FRAME>(stream, defaultHeader, new List<SessionInfo<SESSION>>());
    var buffer = new byte[reader._sessionSize];
    stream.ReadExactly(buffer);
    return SpanReader.ReadStruct<T>(buffer);
  }

  private static long GetDataStart(long sessionStart)
  {
    var defaultHeader = new Header
    {
      Version = 1,
      SampleRate = 1,
      StartTimestamp = 0,
      Metadata = new Metadata(new Dictionary<string, string>())
    };
    var reader = new Reader<SESSION, FRAME>(new MemoryStream(), defaultHeader, new List<SessionInfo<SESSION>>());
    return sessionStart + reader._sessionHeaderSize;
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
