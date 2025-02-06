using System.Runtime.InteropServices;

namespace WeRace.Telemetry;

/// <summary>
/// Provides size calculations for fixed size structures.
/// Each type's size is calculated only once when first accessed.
/// </summary>
internal static class StructSizeHelper<T> where T : struct
{
  /// <summary>
  /// The size of the structure type T.
  /// </summary>
  public static readonly int Size = Marshal.SizeOf<T>();
}

/// <summary>
/// Helper for aligning and calculating sizes for WRTF structures.
/// </summary>
internal static class SizeCalculator
{
  /// <summary>
  /// Calculates the total size of a frame, including header and padding.
  /// </summary>
  /// <typeparam name="FRAME">The frame type with a fixed size structure.</typeparam>
  /// <param name="headerSize">The size of the frame header.</param>
  /// <returns>The total size of the frame.</returns>
  public static int GetTotalFrameSize<FRAME>(int headerSize) where FRAME : struct
  {
    var frameSize = StructSizeHelper<FRAME>.Size;
    return headerSize + frameSize + SpanReader.GetPadding(headerSize + frameSize);
  }
}

/// <summary>
/// Provides cached size calculations for telemetry structures.
/// </summary>
internal static class TelemetrySizeHelper<SESSION_HEADER, SESSION_FOOTER, FRAME> where SESSION_HEADER : struct where SESSION_FOOTER : struct where FRAME : struct {

  public const int FileHeaderSize = 40;

  public static readonly int SessionHeaderSize = SpanReader.GetAlignedSize<SESSION_HEADER>();
  public static readonly int TotalSessionHeaderSize = Magic.MagicSize + SessionHeaderSize;

  public static readonly int SessionFooterSize = SpanReader.GetAlignedSize<SESSION_FOOTER>();
  public static readonly int TotalSessionFooterSize = Magic.MagicSize + 16 + SessionFooterSize;

  public static readonly int FrameHeaderSize = SpanReader.GetAlignedSize<FrameHeader>();
  public static readonly int FrameSize = SpanReader.GetAlignedSize<FRAME>();
  public static readonly int TotalFrameSize = FrameSize + FrameHeaderSize + SpanReader.GetPadding(FrameSize + FrameHeaderSize);
}
