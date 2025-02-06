using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Text;

namespace WeRace.Telemetry;

/// <summary>
/// Handles reading telemetry data from a stream.
/// </summary>
/// <typeparam name="SESSION_HEADER">The session header type with a fixed size structure.</typeparam>
/// <typeparam name="SESSION_FOOTER">The session footer type with a fixed size structure.</typeparam>
/// <typeparam name="FRAME">The frame type with a fixed size structure.</typeparam>
public sealed class Reader<SESSION_HEADER, SESSION_FOOTER, FRAME>
  where SESSION_HEADER : struct
  where SESSION_FOOTER : struct
  where FRAME : struct
{
  // private const int FIXED_HEADER_SIZE = 40;

  // private const int FOOTER_SIZE = 24; // Magic(8) + FrameCount(8) + LastTick(8)
  private const int MetadataOffset = 40;

  private readonly Stream _stream;

  private readonly int _frameHeaderSize;

  private readonly int _totalFrameSize;
  private readonly int _frameSize;

  public Header Header { get; }
  public IReadOnlyList<SessionInfo<SESSION_HEADER, SESSION_FOOTER>> Sessions { get; }

  private Reader(Stream stream, Header header, IReadOnlyList<SessionInfo<SESSION_HEADER, SESSION_FOOTER>> sessions)
  {
    _stream = stream;

    _frameHeaderSize = TelemetrySizeHelper<SESSION_HEADER, SESSION_FOOTER, FRAME>.FrameHeaderSize;
    _frameSize = TelemetrySizeHelper<SESSION_HEADER, SESSION_FOOTER, FRAME>.FrameSize;
    _totalFrameSize = TelemetrySizeHelper<SESSION_HEADER, SESSION_FOOTER, FRAME>.TotalFrameSize;

    Header = header;
    Sessions = sessions;
  }

  /// <summary>
  /// Opens a telemetry data stream for reading.
  /// </summary>
  /// <param name="stream">The stream containing telemetry data. Must be readable and seekable.</param>
  /// <returns>A Reader instance for accessing session and frame data.</returns>
  /// <exception cref="ArgumentException">Thrown if the stream is not readable or seekable.</exception>
  public static Reader<SESSION_HEADER, SESSION_FOOTER, FRAME> Open(Stream stream)
  {
    ArgumentNullException.ThrowIfNull(stream);
    if (!stream.CanRead || !stream.CanSeek)
      throw new ArgumentException("Stream must be readable and seekable", nameof(stream));

    if (stream.Length % 8 != 0)
    {
      throw new InvalidDataException("Stream length must be a multiple of 8");
    }

    var header = ReadHeader(stream);
    var sessions = ReadDocumentFooter(stream);

    return new(stream, header, sessions);
  }

  private static Header ReadHeader(Stream stream)
  {
    Span<byte> headerBuffer = stackalloc byte[TelemetrySizeHelper<SESSION_HEADER, SESSION_FOOTER, FRAME>.FileHeaderSize];
    stream.ReadExactly(headerBuffer);

    if (!SpanReader.TryReadMagic(headerBuffer[..Magic.MagicSize], Magic.FileMagic))
      throw new InvalidDataException("Invalid WRTF file magic number");

    var version = BinaryPrimitives.ReadUInt64LittleEndian(headerBuffer.Slice(8, 8));
    var sampleRate = BinaryPrimitives.ReadUInt64LittleEndian(headerBuffer.Slice(16, 8));
    var timestamp = BinaryPrimitives.ReadUInt64LittleEndian(headerBuffer.Slice(24, 8));
    var metadataCount = BinaryPrimitives.ReadUInt32LittleEndian(headerBuffer.Slice(32, 4));

    var metadata = ReadMetadataEntries(stream, MetadataOffset, metadataCount);

    return new Header
    {
      Version = version,
      SampleRate = sampleRate,
      StartTimestamp = timestamp,
      Metadata = new Metadata(metadata)
    };
  }

  private static FrozenDictionary<string, string> ReadMetadataEntries(Stream stream, long offset, uint count)
  {
    const int stringLengthSize = 4;

    var entries = new Dictionary<string, string>((int)count);
    stream.Position = offset;

    var maxSize = 0;
    var positions = new (int keyStart, int keyLength, int valueStart, int valueLength)[count];
    Span<byte> lengthBuffer = stackalloc byte[stringLengthSize];

    var currentPosition = 0;
    for (var i = 0; i < count; i++)
    {
      stream.ReadExactly(lengthBuffer);
      var keyLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
      if (keyLength < 0)
      {
        throw new InvalidDataException("Invalid metadata key length");
      }

      currentPosition += stringLengthSize;

      var keyStart = currentPosition;

      stream.Seek(keyLength, SeekOrigin.Current);
      currentPosition += keyLength;
      var keyPadding = SpanReader.GetPadding(currentPosition);
      if (keyPadding > 0)
      {
        stream.Seek(keyPadding, SeekOrigin.Current);
        currentPosition += keyPadding;
      }


      stream.ReadExactly(lengthBuffer);
      var valueLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
      if (valueLength < 0)
      {
        throw new InvalidDataException("Invalid metadata value length");
      }

      currentPosition += stringLengthSize;

      var valueStart = currentPosition;
      stream.Seek(valueLength, SeekOrigin.Current);

      currentPosition += valueLength;
      var valuePadding = SpanReader.GetPadding(currentPosition);
      if (valuePadding > 0)
      {
        currentPosition += valuePadding;
        stream.Seek(valuePadding, SeekOrigin.Current);
      }

      positions[i] = (keyStart, keyLength, valueStart, valueLength);
      maxSize = Math.Max(maxSize, Math.Max(keyLength, valueLength));
    }

    // Now read the actual data using a single buffer
    var buffer = new byte[maxSize];

    for (var i = 0; i < count; i++)
    {
      var (keyStart, keyLength, valueStart, valueLength) = positions[i];

      stream.Position = keyStart + offset;
      stream.ReadExactly(buffer.AsSpan(0, keyLength));

      var key = Encoding.UTF8.GetString(buffer, 0, keyLength);

      stream.Position = valueStart + offset;

      stream.ReadExactly(buffer.AsSpan(0, valueLength));
      var value = Encoding.UTF8.GetString(buffer, 0, valueLength);

      entries.Add(key, value);
    }

    return entries.ToFrozenDictionary();
  }

  private static List<SessionInfo<SESSION_HEADER, SESSION_FOOTER>> ReadDocumentFooter(Stream stream)
  {
    const int sessionEntrySize = 24;

    // Read document footer end marker
    var endMagicStart = stream.Length - Magic.MagicSize;

    stream.Position = endMagicStart;
    var magicBuffer = new byte[Magic.MagicSize];
    stream.ReadExactly(magicBuffer);
    if (!SpanReader.TryReadMagic(magicBuffer, Magic.DocumentFooterEndMagic))
      throw new InvalidDataException($"Invalid document end marker");

    // Read number of sessions (8 bytes before end magic)
    stream.Position = endMagicStart - 8;
    var countBuffer = new byte[8];
    stream.ReadExactly(countBuffer);
    var sessionCount = BinaryPrimitives.ReadUInt64LittleEndian(countBuffer);

    // Calculate start of footer section
    var startMagicPos = endMagicStart - 8 - ((long)sessionCount * sessionEntrySize) - Magic.MagicSize;

    // Verify start magic
    stream.Position = startMagicPos;
    stream.ReadExactly(magicBuffer);
    if (!SpanReader.TryReadMagic(magicBuffer, Magic.DocumentFooterStartMagic))
      throw new InvalidDataException($"Invalid document footer start marker");

    // First collect all session positions
    var sessionPositions = new (long headerStart, long footerStart, ulong frameCount)[sessionCount];
    var buffer = new byte[sessionEntrySize];
    stream.Position = startMagicPos + Magic.MagicSize;

    for (var i = 0UL; i < sessionCount; i++)
    {
      stream.ReadExactly(buffer);
      var headerStart = (long)BinaryPrimitives.ReadUInt64LittleEndian(buffer.AsSpan(0, 8));
      var footerStart = (long)BinaryPrimitives.ReadUInt64LittleEndian(buffer.AsSpan(8, 8));
      var frameCount = BinaryPrimitives.ReadUInt64LittleEndian(buffer.AsSpan(16, 8));
      sessionPositions[i] = (headerStart, footerStart, frameCount);
    }

    // Now read all sessions using the collected positions
    var sessions = new List<SessionInfo<SESSION_HEADER, SESSION_FOOTER>>((int)Math.Min(sessionCount, int.MaxValue));

    for (var i = 0UL; i < sessionCount; i++)
    {
      var (headerStart, footerStart, frameCount) = sessionPositions[i];

      // Read session header
      var header = ReadSessionHeader(stream, headerStart, i);
      var dataStart = headerStart + TelemetrySizeHelper<SESSION_HEADER, SESSION_FOOTER, FRAME>.TotalSessionHeaderSize;

      // Read footer
      var (frames, lastTick, footer) = ReadSessionFooter(stream, footerStart, i);

      if (frameCount != frames)
      {
        throw new InvalidDataException($"Invalid session frame count");
      }

      sessions.Add(new SessionInfo<SESSION_HEADER, SESSION_FOOTER>
      {
        Header = header,
        Footer = footer,
        FrameCount = frames,
        LastFrameTick = lastTick,
        StartOffset = headerStart,
        DataOffset = dataStart,
        FooterOffset = footerStart
      });
    }

    return sessions;
  }

  /// <summary>
  /// Enumerates frames within a specified session.
  /// </summary>
  /// <param name="session">The session information from which to read frames.</param>
  /// <returns>An enumerable of frames.</returns>
  public IEnumerable<Frame<FRAME>> GetFrames(SessionInfo<SESSION_HEADER, SESSION_FOOTER> session)
  {
    var buffer = new byte[_totalFrameSize];
    var currentPos = session.DataOffset;
    var endPos = session.FooterOffset;

    for (var i = 0UL; i < session.FrameCount && currentPos + _totalFrameSize <= endPos; i++)
    {
      _stream.Position = currentPos;
      _stream.ReadExactly(buffer);

      var tick = BinaryPrimitives.ReadUInt64LittleEndian(buffer.AsSpan(0, sizeof(ulong)));
      var frame = SpanReader.ReadAlignedStruct<FRAME>(buffer.AsSpan(_frameHeaderSize, _frameSize));

      yield return new Frame<FRAME>
      {
        Header = new FrameHeader(tick),
        Data = frame
      };

      currentPos += _totalFrameSize;
    }
  }

  private static SESSION_HEADER ReadSessionHeader(Stream stream, long position, ulong sessionNumber)
  {
    stream.Position = position;

    var buffer = new byte[Magic.MagicSize];
    stream.ReadExactly(buffer);
    if (!SpanReader.TryReadMagic(buffer, Magic.SessionHeaderMagic))
      throw new InvalidDataException($"Invalid session header magic for session number {sessionNumber}");

    buffer = new byte[TelemetrySizeHelper<SESSION_HEADER, SESSION_FOOTER, FRAME>.SessionHeaderSize];

    stream.ReadExactly(buffer);
    return SpanReader.ReadAlignedStruct<SESSION_HEADER>(buffer);
  }

  private static (ulong FrameCount, ulong LastTick, SESSION_FOOTER Footer) ReadSessionFooter(Stream stream, long position, ulong sessionNumber)
  {
    stream.Position = position;

    var buffer = new byte[Magic.MagicSize];
    stream.ReadExactly(buffer);
    if (!SpanReader.TryReadMagic(buffer, Magic.SessionFooterMagic))
      throw new InvalidDataException($"Invalid session footer magic for session number {sessionNumber}");

    var tickAndCountBuffer = new byte[16];
    stream.ReadExactly(tickAndCountBuffer);
    var lastTick = BinaryPrimitives.ReadUInt64LittleEndian(tickAndCountBuffer.AsSpan(0, 8));
    var frameCount = BinaryPrimitives.ReadUInt64LittleEndian(tickAndCountBuffer.AsSpan(8, 8));

    // Read last tick and frame count
    buffer = new byte[TelemetrySizeHelper<SESSION_HEADER, SESSION_FOOTER, FRAME>.SessionFooterSize];
    stream.ReadExactly(buffer);
    var footer = SpanReader.ReadAlignedStruct<SESSION_FOOTER>(buffer);

    return (frameCount, lastTick, footer);
  }
}
