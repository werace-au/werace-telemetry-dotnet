namespace WeRace.Telemetry;

/// <summary>
/// Represents a dictionary of metadata entries for telemetry data.
/// </summary>
public readonly record struct Metadata(IReadOnlyDictionary<string, string> Entries);
