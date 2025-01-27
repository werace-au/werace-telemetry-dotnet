using System.Runtime.InteropServices;

namespace WeRace.Telemetry;

/// <summary>
/// Provides size calculations for fixed size structures.
/// Each type's size is calculated only once when first accessed.
/// </summary>
internal static class StructSizeHelper<T> where T : struct {
  public static readonly int Size = Marshal.SizeOf<T>();
}

/// <summary>
/// Helper for aligning and calculating sizes for WRTF structures.
/// </summary>
internal static class SizeCalculator {
  public static int GetTotalFrameSize<FRAME>(int headerSize) where FRAME : struct {
    var frameSize = StructSizeHelper<FRAME>.Size;
    return headerSize + frameSize + SpanReader.GetPadding(headerSize + frameSize);
  }
}
