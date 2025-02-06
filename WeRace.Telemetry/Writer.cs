using System.Buffers.Binary;
using System.Text;

namespace WeRace.Telemetry;

/// <summary>
/// Handles writing telemetry data to a stream.
/// </summary>
/// <typeparam name="SESSION_HEADER">The session header type with a fixed size structure.</typeparam>
/// <typeparam name="SESSION_FOOTER"></typeparam>
/// <typeparam name="FRAME">The frame type with a fixed size structure.</typeparam>
public sealed class Writer<SESSION_HEADER, SESSION_FOOTER, FRAME> : IDisposable where SESSION_HEADER : struct where SESSION_FOOTER : struct where FRAME : struct
{
  private readonly Stream _stream;
  private readonly ulong _sampleRate;
  private readonly Dictionary<string, string> _metadata;
  private readonly int _frameSize;
  private readonly int _sessionHeaderSize;
  private readonly int _sessionFooterSize;
  private readonly int _frameHeaderSize;
  private readonly int _totalFrameSize;
  private readonly List<SessionEntry> _sessions;
  private bool _headerWritten;
  private bool _sessionOpen;
  private ulong _currentTick;
  private ulong _frameCount;
  private long _currentSessionStart;
  private readonly byte[] _writeBuffer;

  public Writer(Stream stream, ulong sampleRate, IReadOnlyDictionary<string, string>? metadata = null)
  {
    ArgumentNullException.ThrowIfNull(stream);
    ArgumentOutOfRangeException.ThrowIfZero(sampleRate);

    if (!stream.CanWrite)
      throw new ArgumentException("Stream must be writable", nameof(stream));
    if (!stream.CanSeek)
      throw new ArgumentException("Stream must be seekable", nameof(stream));

    _frameSize = TelemetrySizeHelper<SESSION_HEADER, SESSION_FOOTER, FRAME>.FrameSize;
    _sessionHeaderSize = TelemetrySizeHelper<SESSION_HEADER, SESSION_FOOTER, FRAME>.SessionHeaderSize;
    _sessionFooterSize = TelemetrySizeHelper<SESSION_HEADER, SESSION_FOOTER, FRAME>.SessionFooterSize;
    _frameHeaderSize = TelemetrySizeHelper<SESSION_HEADER, SESSION_FOOTER, FRAME>.FrameHeaderSize;
    _totalFrameSize = TelemetrySizeHelper<SESSION_HEADER, SESSION_FOOTER, FRAME>.TotalFrameSize;

    _stream = stream;
    _sampleRate = sampleRate;
    _metadata = new Dictionary<string, string>(metadata ?? new Dictionary<string, string>());
    _sessions = new List<SessionEntry>();
    _writeBuffer = new byte[Math.Max(1024, _totalFrameSize)];
  }

  /// <summary>
  /// Begins a new telemetry session.
  /// </summary>
  /// <param name="header">The session data to write.</param>
  /// <exception cref="InvalidOperationException">Thrown if a session is already open.</exception>
  public void BeginSession(SESSION_HEADER header)
  {
    if (_sessionOpen)
      throw new InvalidOperationException("Cannot begin a new session while another session is open");

    EnsureHeader();

    _currentSessionStart = _stream.Position;

    // Write session header magic
    _stream.Write(Encoding.ASCII.GetBytes(Magic.SessionHeaderMagic));

    // Write session data with proper alignment
    SpanReader.WriteAlignedStruct(header, _writeBuffer.AsSpan(0, _sessionHeaderSize));
    _stream.Write(_writeBuffer.AsSpan(0, _sessionHeaderSize));

    _sessionOpen = true;
    _currentTick = 0;
    _frameCount = 0;
    _stream.Flush();
  }

  /// <summary>
  /// Writes a frame of telemetry data to the current session.
  /// </summary>
  /// <param name="tick">The frame tick. Must be </param>
  /// <param name="frame">The frame data to write.</param>
  /// <exception cref="InvalidOperationException">Thrown if no session is open.</exception>
  public void WriteFrame(ulong tick, FRAME frame)
  {
    if (!_sessionOpen)
      throw new InvalidOperationException("Cannot write frames without an open session");

    if (tick <= _currentTick && _currentTick != 0)
    {
      throw new InvalidOperationException("Tick must be greater than or equal to the last tick written");
    }

    _frameCount++;
    _currentTick = tick;
    var header = new FrameHeader(tick);

    // Write header
    SpanReader.WriteAlignedStruct(header, _writeBuffer.AsSpan(0, _frameHeaderSize));

    // Write frame data immediately after header
    SpanReader.WriteAlignedStruct(frame, _writeBuffer.AsSpan(_frameHeaderSize, _frameSize));

    // Write full buffer including padding
    _stream.Write(_writeBuffer.AsSpan(0, _totalFrameSize));
  }

  /// <summary>
  /// Ends the current telemetry session.
  /// </summary>
  /// <param name="footer">The session footer data to write.</param>
  /// <exception cref="InvalidOperationException">Thrown if no session is open.</exception>
  public void EndSession(SESSION_FOOTER footer)
  {
    if (!_sessionOpen)
      throw new InvalidOperationException("Cannot end session when no session is open");

    var footerPosition = _stream.Position;

    // Write the magic number, frame count, and last tick in one contiguous block
    var headerSpan = _writeBuffer.AsSpan(0, 24);
    Encoding.ASCII.GetBytes(Magic.SessionFooterMagic).CopyTo(headerSpan[..8]);
    BinaryPrimitives.WriteUInt64LittleEndian(headerSpan[8..16], _currentTick);
    BinaryPrimitives.WriteUInt64LittleEndian(headerSpan[16..24], _frameCount);
    _stream.Write(headerSpan);


    // Write session footer data with proper alignment
    var footerBuffer = _writeBuffer.AsSpan(0, _sessionFooterSize);
    footerBuffer.Clear(); // Zero out the buffer first
    SpanReader.WriteAlignedStruct(footer, footerBuffer);
    _stream.Write(footerBuffer);

    // Record session information for document footer
    _sessions.Add(new SessionEntry
    {
      SessionOffset = _currentSessionStart,
      FooterOffset = footerPosition,
      FrameCount = _frameCount,
    });

    _sessionOpen = false;
  }

  private void WriteDocumentFooter()
  {

    // Write document footer start magic
    _stream.Write(Encoding.ASCII.GetBytes(Magic.DocumentFooterStartMagic));

    // Write session entries
    foreach (var session in _sessions)
    {
      var span = _writeBuffer.AsSpan(0, 24);
      BinaryPrimitives.WriteUInt64LittleEndian(span[..8], (ulong)session.SessionOffset);
      BinaryPrimitives.WriteUInt64LittleEndian(span[8..16], (ulong)session.FooterOffset);
      BinaryPrimitives.WriteUInt64LittleEndian(span[16..], session.FrameCount);
      _stream.Write(span);
    }

    // Write number of sessions
    var sessionCountSpan = _writeBuffer.AsSpan(0, 8);
    BinaryPrimitives.WriteUInt64LittleEndian(sessionCountSpan, (ulong)_sessions.Count);
    _stream.Write(sessionCountSpan);

    // Write document footer end magic
    _stream.Write(Encoding.ASCII.GetBytes(Magic.DocumentFooterEndMagic));

    _stream.Flush();
  }

  private void EnsureHeader()
  {
    if (_headerWritten) return;

    // Write magic number
    _stream.Write(Encoding.ASCII.GetBytes(Magic.FileMagic));

    var span = _writeBuffer.AsSpan();

    // Write version, sample rate, timestamp
    BinaryPrimitives.WriteUInt64LittleEndian(span, 1UL);
    _stream.Write(span[..8]);

    BinaryPrimitives.WriteUInt64LittleEndian(span, _sampleRate);
    _stream.Write(span[..8]);

    var timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000;
    BinaryPrimitives.WriteUInt64LittleEndian(span, timestamp);
    _stream.Write(span[..8]);

    // Write metadata count and reserved field
    BinaryPrimitives.WriteUInt32LittleEndian(span, (uint)_metadata.Count);
    _stream.Write(span[..4]);
    BinaryPrimitives.WriteUInt32LittleEndian(span, 0);
    _stream.Write(span[..4]);

    var currentPosition = 40L;
    foreach (var (key, value) in _metadata)
    {
      var keyBytes = Encoding.UTF8.GetBytes(key);
      var valueBytes = Encoding.UTF8.GetBytes(value);

      BinaryPrimitives.WriteInt32LittleEndian(span, keyBytes.Length);
      _stream.Write(span[..4]);
      _stream.Write(keyBytes);

      currentPosition += 4 + keyBytes.Length;
      var keyPadding = SpanReader.GetPadding((int)currentPosition);
      if (keyPadding > 0)
      {
        _stream.Write(new byte[keyPadding]);
        currentPosition += keyPadding;
      }

      BinaryPrimitives.WriteInt32LittleEndian(span, valueBytes.Length);
      _stream.Write(span[..4]);
      _stream.Write(valueBytes);

      currentPosition += 4 + valueBytes.Length;
      var valuePadding = SpanReader.GetPadding((int)currentPosition);
      if (valuePadding > 0)
      {
        _stream.Write(new byte[valuePadding]);
        currentPosition += valuePadding;
      }
    }

    _headerWritten = true;
  }

  public void Dispose()
  {
    if (_sessionOpen)
    {
      // KAO - Do our best to ensure that the file is valid
      EndSession(new());
    }

    WriteDocumentFooter();
  }
}
