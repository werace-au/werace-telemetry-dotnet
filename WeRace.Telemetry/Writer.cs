using System.Buffers.Binary;
using System.Text;

namespace WeRace.Telemetry;

/// <summary>
/// Handles writing telemetry data to a stream.
/// </summary>
/// <typeparam name="SESSION">The session type with a fixed size structure.</typeparam>
/// <typeparam name="FRAME">The frame type with a fixed size structure.</typeparam>
public sealed class Writer<SESSION, FRAME> : IDisposable where SESSION : struct where FRAME : struct
{
  private readonly Stream _stream;
  private readonly ulong _sampleRate;
  private readonly Dictionary<string, string> _metadata;
  private readonly int _frameSize;
  private readonly int _sessionSize;
  private readonly int _headerSize;
  private readonly int _totalFrameSize;
  private readonly List<SessionEntry> _sessions;
  private bool _headerWritten;
  private bool _sessionOpen;
  private ulong _currentTick;
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

    _stream = stream;
    _sampleRate = sampleRate;
    _metadata = new Dictionary<string, string>(metadata ?? new Dictionary<string, string>());
    _frameSize = TelemetrySizeHelper<SESSION, FRAME>.FrameSize;
    _sessionSize = TelemetrySizeHelper<SESSION, FRAME>.SessionSize;
    _headerSize = TelemetrySizeHelper<SESSION, FRAME>.HeaderSize;
    _totalFrameSize = TelemetrySizeHelper<SESSION, FRAME>.TotalFrameSize;
    _sessions = new List<SessionEntry>();
    _writeBuffer = new byte[Math.Max(1024, _totalFrameSize)];
  }

  /// <summary>
  /// Begins a new telemetry session.
  /// </summary>
  /// <param name="sessionData">The session data to write.</param>
  /// <exception cref="InvalidOperationException">Thrown if a session is already open.</exception>
  public void BeginSession(SESSION sessionData)
  {
    if (_sessionOpen)
      throw new InvalidOperationException("Cannot begin a new session while another session is open");

    EnsureHeader();

    _currentSessionStart = _stream.Position;

    // Write session header magic
    _stream.Write(Encoding.ASCII.GetBytes(Magic.SessionMagic));

    // Write session data with proper alignment
    var sessionSize = _sessionSize;
    SpanReader.WriteStruct(sessionData, _writeBuffer.AsSpan(0, sessionSize));
    _stream.Write(_writeBuffer.AsSpan(0, sessionSize));

    // Add alignment padding
    var padding = SpanReader.GetPadding(8 + sessionSize);
    if (padding > 0)
      _stream.Write(new byte[padding]);

    _sessionOpen = true;
    _currentTick = 0;
    _stream.Flush();
  }

  /// <summary>
  /// Writes a frame of telemetry data to the current session.
  /// </summary>
  /// <param name="frame">The frame data to write.</param>
  /// <exception cref="InvalidOperationException">Thrown if no session is open.</exception>
  public void WriteFrame(FRAME frame)
  {
    if (!_sessionOpen)
      throw new InvalidOperationException("Cannot write frames without an open session");

    var header = new FrameHeader(_currentTick++);
    var headerSize = _headerSize;
    var frameSize = _frameSize;

    // Write header
    SpanReader.WriteStruct(header, _writeBuffer.AsSpan(0, headerSize));

    // Write frame data immediately after header
    SpanReader.WriteStruct(frame, _writeBuffer.AsSpan(headerSize, frameSize));

    // Write full buffer including padding
    _stream.Write(_writeBuffer.AsSpan(0, _totalFrameSize));
  }

  /// <summary>
  /// Ends the current telemetry session.
  /// </summary>
  /// <exception cref="InvalidOperationException">Thrown if no session is open.</exception>
  public void EndSession()
  {
    if (!_sessionOpen)
      throw new InvalidOperationException("Cannot end session when no session is open");

    var footerPosition = _stream.Position;

    // Write footer magic
    _stream.Write(Encoding.ASCII.GetBytes(Magic.SessionFooterMagic));

    // Write frame count and last tick
    var span = _writeBuffer.AsSpan(0, 16);
    BinaryPrimitives.WriteUInt64LittleEndian(span[..8], _currentTick);
    BinaryPrimitives.WriteUInt64LittleEndian(span[8..], _currentTick > 0 ? _currentTick - 1 : 0);
    _stream.Write(span);

    // Record session information for document footer
    _sessions.Add(new SessionEntry
    {
      SessionOffset = _currentSessionStart,
      FooterOffset = footerPosition,
      FrameCount = _currentTick
    });

    _sessionOpen = false;
    _stream.Flush();
  }

  public void Dispose()
  {
    if (_sessionOpen)
    {
      try
      {
        EndSession();
      }
      catch
      {
        /* Best effort */
      }
    }

    WriteDocumentFooter();
  }

  private void WriteDocumentFooter()
  {
    if (_sessions.Count == 0) return;

    // Write document footer start magic
    _stream.Write(Encoding.ASCII.GetBytes(Magic.DocumentFooterStartMagic));

    // Write session entries
    foreach (var session in _sessions)
    {
      var span = _writeBuffer.AsSpan(0, 24);
      BinaryPrimitives.WriteInt64LittleEndian(span[..8], session.SessionOffset);
      BinaryPrimitives.WriteInt64LittleEndian(span[8..16], session.FooterOffset);
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
    _stream.Flush();
  }
}
