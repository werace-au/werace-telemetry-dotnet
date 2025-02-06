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
  public const int MagicSize = 8;

  /// <summary>
  /// Magic string for identifying a telemetry file.
  /// </summary>
  public const string FileMagic = "WRTF0001";

  /// <summary>
  /// Magic string for identifying the start of a session within a telemetry file.
  /// </summary>
  public const string SessionHeaderMagic = "WRSE0001";

  /// <summary>
  /// Magic string for identifying the footer of a session within a telemetry file.
  /// </summary>
  public const string SessionFooterMagic = "WRSF0001";

  /// <summary>
  /// Magic string for identifying the start of a document footer within a telemetry file.
  /// </summary>
  public const string DocumentFooterStartMagic = "WRDF0001";

  /// <summary>
  /// Magic string for identifying the end of a document footer within a telemetry file.
  /// </summary>
  public const string DocumentFooterEndMagic = "WRDE0001";

  static Magic()
  {
    // Verify all magic strings are exactly MAGIC_SIZE bytes
    Debug.Assert(Encoding.ASCII.GetByteCount(FileMagic) == MagicSize);
    Debug.Assert(Encoding.ASCII.GetByteCount(SessionHeaderMagic) == MagicSize);
    Debug.Assert(Encoding.ASCII.GetByteCount(SessionFooterMagic) == MagicSize);
    Debug.Assert(Encoding.ASCII.GetByteCount(DocumentFooterStartMagic) == MagicSize);
    Debug.Assert(Encoding.ASCII.GetByteCount(DocumentFooterEndMagic) == MagicSize);
  }
}
