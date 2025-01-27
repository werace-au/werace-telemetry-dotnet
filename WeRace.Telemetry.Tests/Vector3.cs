using System.Runtime.InteropServices;

namespace WeRace.Telemetry.Tests;

[StructLayout(LayoutKind.Sequential)]
public struct Vector3
{
  public float X;
  public float Y;
  public float Z;
}
