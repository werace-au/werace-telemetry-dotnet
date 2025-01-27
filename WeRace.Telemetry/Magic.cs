namespace WeRace.Telemetry;
using System;
using System.Diagnostics;
using System.Text;

/// <summary>
/// Contains magic strings used for identifying different sections of telemetry data files.
/// </summary>
public static class Magic
{
  /// <summary>
  /// The size of magic strings in bytes.
  /// </summary>
  public const int MAGIC_SIZE = 8;

  /// <summary>
  /// Magic string for identifying a telemetry file.
  /// </summary>
  public const string FileMagic = "WRTF0001";

  /// <summary>
  /// Magic string for identifying the start of a session within a telemetry file.
  /// </summary>
  public const string SessionMagic = "SESS0001";

  /// <summary>
  /// Magic string for identifying the footer of a session within a telemetry file.
  /// </summary>
  public const string SessionFooterMagic = "FOOT0001";

  static Magic()
  {
    // Verify all magic strings are exactly MAGIC_SIZE bytes
    Debug.Assert(Encoding.ASCII.GetByteCount(FileMagic) == MAGIC_SIZE);
    Debug.Assert(Encoding.ASCII.GetByteCount(SessionMagic) == MAGIC_SIZE);
    Debug.Assert(Encoding.ASCII.GetByteCount(SessionFooterMagic) == MAGIC_SIZE);
  }
}
