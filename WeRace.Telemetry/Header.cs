namespace WeRace.Telemetry;

/// <summary>
/// Represents the header information of a WRTF file, including version, sample rate, and metadata.
/// </summary>
public readonly record struct Header
{
  /// <summary>
  /// The version of the WRTF file format.
  /// </summary>
  public required ulong Version { get; init; }

  /// <summary>
  /// The sample rate of the telemetry data.
  /// </summary>
  public required ulong SampleRate { get; init; }

  /// <summary>
  /// The start timestamp of the telemetry session.
  /// </summary>
  public required ulong StartTimestamp { get; init; }

  /// <summary>
  /// The metadata associated with the telemetry session.
  /// </summary>
  public required Metadata Metadata { get; init; }
}
