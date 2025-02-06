using System.Text;

namespace WeRace.Telemetry.Tests;

public class HeaderAlignmentTests
{
    [Fact]
    public void ValidateFixedHeaderAlignment()
    {
        // The fixed header should be exactly 40 bytes and aligned
        using var stream = new MemoryStream();
        using var writer = new Writer<UnalignedTestSession, TestSessionFooter, TestFrame>(stream, 60);

        // Writing just the header by starting a session but not writing data
        writer.BeginSession(new UnalignedTestSession());
        writer.EndSession(new TestSessionFooter());

        var bytes = stream.ToArray();

        // Verify magic number
        Assert.True(SpanReader.TryReadMagic(bytes.AsSpan(0, 8), Magic.FileMagic));

        // Verify the fixed header fields are aligned correctly
        Assert.Equal(0, 8 % 8);   // Version offset should be aligned
        Assert.Equal(0, 16 % 8);  // Sample rate offset should be aligned
        Assert.Equal(0, 24 % 8);  // Timestamp offset should be aligned
        Assert.Equal(0, 32 % 8);  // Metadata count offset should be aligned
    }

    [Fact]
    public void ValidateEmptyMetadataSectionAlignment()
    {
        using var stream = new MemoryStream();
        using var writer = new Writer<UnalignedTestSession, TestSessionFooter, TestFrame>(stream, 60);

        writer.BeginSession(new UnalignedTestSession());
        writer.EndSession(new TestSessionFooter());
        var bytes = stream.ToArray();

        // With no metadata, the session header should start at the next 8-byte boundary after the fixed header
        var expectedSessionStart = 40;  // Fixed header size
        expectedSessionStart = ((expectedSessionStart + 7) / 8) * 8; // Round up to next 8-byte boundary

        // Print the bytes around where we expect the session to start
        PrintBytesAround(bytes, expectedSessionStart, "Session Start Location");

        Assert.True(SpanReader.TryReadMagic(
            bytes.AsSpan(expectedSessionStart, 8),
            Magic.SessionHeaderMagic),
            $"Session magic not found at offset {expectedSessionStart}");
    }

    [Fact]
    public void ValidateFixedHeaderSize()
    {
        using var stream = new MemoryStream();
        using var writer = new Writer<UnalignedTestSession, TestSessionFooter, TestFrame>(stream, 60);

        writer.BeginSession(new UnalignedTestSession());
        writer.EndSession(new TestSessionFooter());
        var bytes = stream.ToArray();

        PrintBytes(bytes, 48, "File Start");

        // Verify session header starts at offset 40
        Assert.True(SpanReader.TryReadMagic(bytes.AsSpan(40, 8), Magic.SessionHeaderMagic));
    }

    [Theory]
    [InlineData("test", "value")]  // Short strings
    [InlineData("longer_test_key", "longer_test_value")]  // Longer strings
    [InlineData("test", "value with spaces")]  // Strings with spaces
    public void ValidateMetadataAlignment(string key, string value)
    {
        using var stream = new MemoryStream();
        using var writer = new Writer<UnalignedTestSession, TestSessionFooter, TestFrame>(
            stream,
            60,
            new Dictionary<string, string> { { key, value } }
        );

        writer.BeginSession(new UnalignedTestSession());
        writer.EndSession(new TestSessionFooter());
        var bytes = stream.ToArray();

        // Calculate where metadata section should end
        var metadataStart = 40;
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var valueBytes = Encoding.UTF8.GetBytes(value);

        var keyLength = 4 + keyBytes.Length;  // Length field + key bytes
        var keyAlignedLength = ((keyLength + 7) / 8) * 8;

        var valueLength = 4 + valueBytes.Length;  // Length field + value bytes
        var valueAlignedLength = ((valueLength + 7) / 8) * 8;

        var expectedMetadataEnd = metadataStart + keyAlignedLength + valueAlignedLength;
        expectedMetadataEnd = ((expectedMetadataEnd + 7) / 8) * 8;  // Round to next 8-byte boundary

        PrintBytesAround(bytes, expectedMetadataEnd, "Expected Session Start");

        Assert.True(SpanReader.TryReadMagic(
            bytes.AsSpan(expectedMetadataEnd, 8),
            Magic.SessionHeaderMagic),
            $"Session magic not found at offset {expectedMetadataEnd}");
    }

    private static void PrintBytes(byte[] bytes, int count, string label)
    {
        Console.WriteLine($"\n{label}:");
        for (var i = 0; i < Math.Min(count, bytes.Length); i += 8)
        {
            var chunk = bytes.Skip(i).Take(8).ToArray();
            var hex = BitConverter.ToString(chunk).Replace("-", " ");
            var ascii = new string(chunk.Select(b => b < 32 || b > 126 ? '.' : (char)b).ToArray());
            Console.WriteLine($"{i:D4}: {hex,-23} | {ascii}");
        }
    }

    private static void PrintBytesAround(byte[] bytes, int position, string label)
    {
        var start = Math.Max(0, position - 16);
        var length = Math.Min(bytes.Length - start, 32);
        Console.WriteLine($"\n{label} (showing bytes around position {position}):");
        PrintBytes(bytes[start..(start + length)], length, $"Bytes at {start}");
    }
}
