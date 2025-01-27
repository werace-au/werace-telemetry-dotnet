namespace WeRace.Telemetry;

/// <summary>
/// Represents combined session information including header and footer data.
/// </summary>
/// <typeparam name="SESSION">The session type with a fixed size structure.</typeparam>
public readonly record struct SessionInfo<SESSION> where SESSION : struct
{
  /// <summary>
  /// The session data.
  /// </summary>
  public required SESSION Data { get; init; }

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
}
