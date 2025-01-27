namespace WeRace.Telemetry;

/// <summary>
/// Represents the header information of a WRTF file.
/// </summary>
public readonly record struct Header {
  public required ulong Version { get; init; }
  public required ulong SampleRate { get; init; }
  public required ulong StartTimestamp { get; init; }
  public required Metadata Metadata { get; init; }
}
