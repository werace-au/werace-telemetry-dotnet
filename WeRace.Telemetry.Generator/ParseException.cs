namespace WeRace.Telemetry.Generator;

/// <summary>
/// Represents errors encountered while parsing telemetry definitions
/// </summary>
public class ParseException : Exception {
  public ParseException(string message) : base(message) { }
  public ParseException(string message, Exception innerException) : base(message, innerException) { }
}
