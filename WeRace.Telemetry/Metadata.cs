namespace WeRace.Telemetry;

public readonly record struct Metadata(IReadOnlyDictionary<string, string> Entries);
