using System.Runtime.InteropServices;

namespace WeRace.Telemetry.Tests;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct UnalignedTestSession
{
  public int Id;           // 4 bytes
  public byte Name1;       // 5 bytes for name
  public byte Name2;
  public byte Name3;
  public byte Name4;
  public byte Name5;
  public short Type;       // 2 bytes
  public byte Padding;     // 1 byte padding
}
