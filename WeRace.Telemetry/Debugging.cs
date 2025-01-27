namespace WeRace.Telemetry;

/// <summary>
/// Provides utility methods for debugging telemetry data streams.
/// </summary>
public class Debugging
{
  /// <summary>
  /// Dumps the structure of a file stream to the console in both hexadecimal and ASCII formats.
  /// </summary>
  /// <param name="stream">The stream to be read and dumped. The stream's position will be reset to zero.</param>
  /// <example>
  /// <code>
  /// using (var stream = new FileStream("path/to/file", FileMode.Open))
  /// {
  ///     Debugging.DumpFileStructure(stream);
  /// }
  /// </code>
  /// </example>
  public static void DumpFileStructure(Stream stream)
  {
    stream.Position = 0;
    var buffer = new byte[8];
    var position = 0L;

    Console.WriteLine("\nFile Structure Dump:");
    Console.WriteLine("====================");

    while (position < stream.Length)
    {
      stream.Position = position;
      var bytesRead = stream.Read(buffer);
      if (bytesRead == 0) break;

      var hex = BitConverter.ToString(buffer, 0, bytesRead).Replace("-", " ");
      var ascii = new string(buffer.Take(bytesRead)
        .Select(b => b is >= 32 and <= 126 ? (char)b : '.')
        .ToArray());

      Console.WriteLine($"{position,8:X8}: {hex,-24} | {ascii}");
      position += 8;
    }
    Console.WriteLine("====================\n");
  }
}
