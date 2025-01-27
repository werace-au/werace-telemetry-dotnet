namespace WeRace.Telemetry;

/// <summary>
/// Represents combined session information including header and footer data.
/// </summary>
/// <typeparam name="SESSION">The session type with a fixed size structure.</typeparam>
public readonly record struct SessionInfo<SESSION> where SESSION : struct {
  public required SESSION Data { get; init; }
  public required ulong FrameCount { get; init; }
  public required ulong LastFrameTick { get; init; }
  public required long StartOffset { get; init; }
  public required long DataOffset { get; init; }
}
