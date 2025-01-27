using System.Runtime.InteropServices;

namespace WeRace.Telemetry.Tests;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct UnalignedTestFrame
{
  public bool Flag;        // 1 byte
  public byte Padding1;    // 1 byte padding
  public short SmallValue; // 2 bytes
  public int NormalValue;  // 4 bytes
  public byte Data1;       // 1 byte
  public byte Data2;       // 1 byte
  public byte Data3;       // 1 byte
  public byte Padding2;    // 1 byte padding
}
