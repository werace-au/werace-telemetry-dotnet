namespace WeRace.Telemetry;

/// <summary>
/// Represents a frame with its header and data.
/// </summary>
/// <typeparam name="FRAME">The frame type with a fixed size structure.</typeparam>
public readonly record struct Frame<FRAME> where FRAME : struct
{
  /// <summary>
  /// The header information for the frame.
  /// </summary>
  public required FrameHeader Header { get; init; }

  /// <summary>
  /// The data contained in the frame.
  /// </summary>
  public required FRAME Data { get; init; }
}
