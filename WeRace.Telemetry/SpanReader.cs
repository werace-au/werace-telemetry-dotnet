using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace WeRace.Telemetry;

/// <summary>
/// Provides utility methods for reading structured data from byte spans.
/// </summary>
public static class SpanReader
{
  private static readonly ConcurrentDictionary<Type, int> SizeCache = [];

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static int GetStructSize<T>() where T : struct =>
      SizeCache.GetOrAdd(typeof(T), _ => Marshal.SizeOf<T>());

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static int GetAlignedSize<T>() where T : struct
  {
    var size = GetStructSize<T>();
    return ((size + 7) / 8) * 8; // Round up to nearest 8 bytes
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static int GetPadding(int size) => (8 - (size % 8)) % 8;

  /// <summary>
  /// Attempts to read a magic string from a span and compare it to an expected value.
  /// </summary>
  /// <param name="span">The span containing the data to check.</param>
  /// <param name="expected">The expected magic string.</param>
  /// <returns>True if the magic string matches; otherwise, false.</returns>
  public static bool TryReadMagic(ReadOnlySpan<byte> span, string expected)
  {
    if (span.Length < expected.Length) return false;
    return span[..expected.Length].SequenceEqual(Encoding.ASCII.GetBytes(expected));
  }

  /// <summary>
  /// Reads a UTF-8 encoded string from a span.
  /// </summary>
  /// <param name="data">The span containing the string data.</param>
  /// <returns>The decoded string.</returns>
  public static string ReadString(ReadOnlySpan<byte> data)
  {
    var length = BinaryPrimitives.ReadInt32LittleEndian(data);
    return Encoding.UTF8.GetString(data[4..(4 + length)]);
  }

  /// <summary>
  /// Reads a structure of type T from a span.
  /// </summary>
  /// <typeparam name="T">The type of the structure to read.</typeparam>
  /// <param name="source">The span containing the structure data.</param>
  /// <returns>The structure read from the span.</returns>
  /// <exception cref="ArgumentException">Thrown if the source span is too small.</exception>
  public static unsafe T ReadStruct<T>(ReadOnlySpan<byte> source) where T : struct
  {
    var actualSize = GetStructSize<T>();
    var alignedSize = GetAlignedSize<T>();

    if (source.Length < alignedSize)
      throw new ArgumentException($"Source buffer too small. Need {alignedSize} bytes.", nameof(source));

    fixed (byte* ptr = source)
    {
      // Create aligned buffer if needed
      if ((ulong)ptr % 8 != 0)
      {
        var aligned = stackalloc byte[alignedSize];
        source[..actualSize].CopyTo(new Span<byte>(aligned, actualSize));
        return Marshal.PtrToStructure<T>((IntPtr)aligned);
      }
      return Marshal.PtrToStructure<T>((IntPtr)ptr);
    }
  }

  public static unsafe void WriteStruct<T>(T value, Span<byte> destination) where T : struct
  {
    var size = GetStructSize<T>();
    if (destination.Length < size)
      throw new ArgumentException($"Destination buffer too small. Need {size} bytes.", nameof(destination));

    fixed (byte* ptr = destination)
    {
      Marshal.StructureToPtr(value, (IntPtr)ptr, false);
    }
  }
}
