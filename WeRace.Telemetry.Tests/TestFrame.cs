using System.Runtime.InteropServices;

namespace WeRace.Telemetry.Tests;

[StructLayout(LayoutKind.Sequential, Pack = 8)]
public struct TestFrame
{
  public float Speed;
  public float RPM;
  public float Throttle;
  public float Brake;
  public float Steering;
  public Vector3 Position;
  public Vector3 Rotation;
}
