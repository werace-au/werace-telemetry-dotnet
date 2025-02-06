namespace WeRace.Telemetry;

/// <summary>
/// Represents combined session information including header and footer data.
/// </summary>
/// <typeparam name="SESSION_HEADER">The session header type with a fixed size structure.</typeparam>
/// <typeparam name="SESSION_FOOTER">The session footer type with a fixed size structure.</typeparam>
public readonly record struct SessionInfo<SESSION_HEADER, SESSION_FOOTER>
    where SESSION_HEADER : struct
    where SESSION_FOOTER : struct
{
  /// <summary>
  /// The session header data.
  /// </summary>
  public required SESSION_HEADER Header { get; init; }

  /// <summary>
  /// The session footer data.
  /// </summary>
  public required SESSION_FOOTER Footer { get; init; }

  /// <summary>
  /// The number of frames in the session.
  /// </summary>
  public required ulong FrameCount { get; init; }

  /// <summary>
  /// The tick count of the last frame in the session.
  /// </summary>
  public required ulong LastFrameTick { get; init; }

  /// <summary>
  /// The byte offset in the stream where the session starts.
  /// </summary>
  public required long StartOffset { get; init; }

  /// <summary>
  /// The byte offset in the stream where the session data starts.
  /// </summary>
  public required long DataOffset { get; init; }

  /// <summary>
  /// The byte offset in the stream where the session footer starts.
  /// </summary>
  public required long FooterOffset { get; init; }
}
