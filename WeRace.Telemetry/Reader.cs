using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Text;

namespace WeRace.Telemetry;

public sealed class Reader<SESSION, FRAME> where SESSION : struct where FRAME : struct {
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

  public Header Header { get; }
  public IReadOnlyList<SessionInfo<SESSION>> Sessions { get; }

  private Reader(Stream stream, Header header, IReadOnlyList<SessionInfo<SESSION>> sessions) {
    _stream = stream;
    Header = header;
    Sessions = sessions;
    _frameSize = SpanReader.GetAlignedSize<FRAME>();
    _headerSize = SpanReader.GetAlignedSize<FrameHeader>();
    _totalFrameSize = _frameSize + _headerSize + SpanReader.GetPadding(_frameSize + _headerSize);
  }

  public static Reader<SESSION, FRAME> Open(Stream stream) {
    ArgumentNullException.ThrowIfNull(stream);
    if (!stream.CanRead || !stream.CanSeek)
      throw new ArgumentException("Stream must be readable and seekable", nameof(stream));

    var header = ReadHeader(stream);
    var sessions = ReadSessionsFromEnd(stream);

    return new Reader<SESSION, FRAME>(stream, header, sessions);
  }

  private static Header ReadHeader(Stream stream) {
    Span<byte> headerBuffer = stackalloc byte[FIXED_HEADER_SIZE];
    stream.ReadExactly(headerBuffer);

    if (!SpanReader.TryReadMagic(headerBuffer[..MAGIC_SIZE], Magic.FileMagic))
      throw new InvalidDataException("Invalid WRTF file magic number");

    var version = BinaryPrimitives.ReadUInt64LittleEndian(headerBuffer.Slice(8, 8));
    var sampleRate = BinaryPrimitives.ReadUInt64LittleEndian(headerBuffer.Slice(16, 8));
    var timestamp = BinaryPrimitives.ReadUInt64LittleEndian(headerBuffer.Slice(24, 8));
    var metadataCount = BinaryPrimitives.ReadUInt32LittleEndian(headerBuffer.Slice(32, 4));

    var metadata = ReadMetadataEntries(stream, METADATA_START_OFFSET, metadataCount);
    return new Header {
      Version = version,
      SampleRate = sampleRate,
      StartTimestamp = timestamp,
      Metadata = new Metadata(metadata)
    };
  }

  private static IReadOnlyDictionary<string, string> ReadMetadataEntries(Stream stream, long offset, uint count) {
    var entries = new Dictionary<string, string>((int)count);
    stream.Position = offset;
    var buffer = new byte[1024];
    Span<byte> lengthBuffer = stackalloc byte[LENGTH_FIELD_SIZE];

    for (var i = 0; i < count; i++) {
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

  private static List<SessionInfo<SESSION>> ReadSessionsFromEnd(Stream stream) {
    var sessions = new List<SessionInfo<SESSION>>();
    var magic = new byte[MAGIC_SIZE];
    var footerData = new byte[FOOTER_SIZE - MAGIC_SIZE];
    var minPos = FIXED_HEADER_SIZE + GetMetadataSize(stream);
    var currentPos = stream.Length;
    var frameSize = SpanReader.GetAlignedSize<FRAME>();
    var frameHeaderSize = SpanReader.GetAlignedSize<FrameHeader>();
    var totalFrameSize = frameSize + frameHeaderSize + SpanReader.GetPadding(frameSize + frameHeaderSize);
    var sessionHeaderSize = MAGIC_SIZE + SpanReader.GetAlignedSize<SESSION>() +
                            SpanReader.GetPadding(MAGIC_SIZE + SpanReader.GetAlignedSize<SESSION>());

    while (currentPos > minPos) {
      if (currentPos < FOOTER_SIZE + minPos) break;

      stream.Position = currentPos - FOOTER_SIZE;
      stream.ReadExactly(magic);

      if (SpanReader.TryReadMagic(magic, Magic.SessionFooterMagic)) {
        stream.ReadExactly(footerData);
        var frameCount = BinaryPrimitives.ReadUInt64LittleEndian(footerData.AsSpan(0, 8));
        var lastFrameTick = BinaryPrimitives.ReadUInt64LittleEndian(footerData.AsSpan(8, 8));

        // Calculate session start based on frame count and sizes
        var dataSize = (long)frameCount * totalFrameSize;
        var sessionStart = currentPos - FOOTER_SIZE - dataSize - sessionHeaderSize;

        if (sessionStart >= minPos) {
          // Verify session header
          stream.Position = sessionStart;
          stream.ReadExactly(magic);
          if (SpanReader.TryReadMagic(magic, Magic.SessionMagic)) {
            var session = ReadSessionData<SESSION>(stream, sessionStart);
            var dataStart = sessionStart + sessionHeaderSize;

            sessions.Insert(0, new SessionInfo<SESSION> {
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

  private static int GetMetadataSize(Stream stream) {
    stream.Position = METADATA_COUNT_OFFSET;
    Span<byte> countBuffer = stackalloc byte[LENGTH_FIELD_SIZE];
    stream.ReadExactly(countBuffer);
    var count = BinaryPrimitives.ReadUInt32LittleEndian(countBuffer);

    var currentPos = METADATA_START_OFFSET;
    for (var i = 0; i < count; i++) {
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

  private static T ReadSessionData<T>(Stream stream, long position) where T : struct {
    stream.Position = position + MAGIC_SIZE;
    var buffer = new byte[SpanReader.GetAlignedSize<T>()];
    stream.ReadExactly(buffer);
    return SpanReader.ReadStruct<T>(buffer);
  }

  private static long GetDataStart(long sessionStart) =>
    sessionStart + MAGIC_SIZE + SpanReader.GetAlignedSize<SESSION>() +
    SpanReader.GetPadding(MAGIC_SIZE + SpanReader.GetAlignedSize<SESSION>());

  public IEnumerable<Frame<FRAME>> GetFrames(SessionInfo<SESSION> session) {
    if (session.FrameCount == 0) yield break;

    _stream.Position = session.DataOffset;
    var buffer = new byte[_totalFrameSize];

    for (var i = 0L; i < (long)session.FrameCount; i++) {
      _stream.ReadExactly(buffer);
      var header = SpanReader.ReadStruct<FrameHeader>(buffer.AsSpan(0, _headerSize));
      var frameData = SpanReader.ReadStruct<FRAME>(buffer.AsSpan(_headerSize, _frameSize));

      yield return new Frame<FRAME> {
        Header = header,
        Data = frameData
      };
    }
  }
}
