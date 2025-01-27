namespace WeRace.Telemetry;

/// <summary>
/// Represents a frame with its header and data.
/// </summary>
/// <typeparam name="FRAME">The frame type with a fixed size structure.</typeparam>
public readonly record struct Frame<FRAME> where FRAME : struct {
  public required FrameHeader Header { get; init; }
  public required FRAME Data { get; init; }
}
