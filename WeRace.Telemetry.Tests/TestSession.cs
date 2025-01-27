using System.Runtime.InteropServices;

namespace WeRace.Telemetry.Tests;

[StructLayout(LayoutKind.Sequential, Pack = 8)]
public struct TestSession {
  public int SessionId;
  public int TrackId;
  public int CarId;
}
