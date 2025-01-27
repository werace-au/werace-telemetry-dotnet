using System.Runtime.InteropServices;

namespace WeRace.Telemetry;

/// <summary>
/// Frame header information.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly struct FrameHeader(ulong tickCount) {
  public readonly ulong TickCount = tickCount;
}
