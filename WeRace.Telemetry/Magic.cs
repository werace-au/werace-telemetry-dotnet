namespace WeRace.Telemetry;

/// <summary>
/// Contains magic strings used for identifying different sections of telemetry data files.
/// </summary>
public static class Magic
{
  /// <summary>
  /// Magic string for identifying the start of a telemetry file.
  /// </summary>
  public const string FileMagic = "WRTF0001";

  /// <summary>
  /// Magic string for identifying the start of a session within a telemetry file.
  /// </summary>
  public const string SessionMagic = "WRSE0001";

  /// <summary>
  /// Magic string for identifying the footer of a session within a telemetry file.
  /// </summary>
  public const string SessionFooterMagic = "WRSF0001";
}
