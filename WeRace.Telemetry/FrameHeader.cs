using System.Runtime.InteropServices;

namespace WeRace.Telemetry;

/// <summary>
/// Frame header information, containing the tick count for a frame.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly struct FrameHeader(ulong tickCount)
{
  /// <summary>
  /// The tick count associated with the frame.
  /// </summary>
  public readonly ulong TickCount = tickCount;
}
