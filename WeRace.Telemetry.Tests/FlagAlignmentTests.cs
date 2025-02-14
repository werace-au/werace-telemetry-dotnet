using System.Runtime.InteropServices;

namespace WeRace.Telemetry.Tests;

[StructLayout(LayoutKind.Sequential, Pack = 8)]
public struct FlagTestFrame
{
    public ulong Flags1;           // 8 bytes
    public byte SmallValue;        // 1 byte
    public byte Padding1;          // 1 byte padding
    public byte Padding2;          // 1 byte padding
    public byte Padding3;          // 1 byte padding
    public uint Flags2;            // 4 bytes
    public short SmallValue2;      // 2 bytes
    public byte Padding4;          // 1 byte padding
    public byte Padding5;          // 1 byte padding
    public ulong Flags3;           // 8 bytes
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
public struct FlagTestSession
{
    public int SessionId;          // 4 bytes
    public uint SessionFlags;      // 4 bytes
    public ulong ExtendedFlags;    // 8 bytes
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
public struct FlagTestSessionFooter
{
    public ulong FinalFlags;       // 8 bytes
    public uint StatusFlags;       // 4 bytes
    public uint Reserved;          // 4 bytes padding
}

public class FlagAlignmentTests
{
    private static readonly DateTime TestStartTime = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Dictionary<string, string> TestMetadata = new() { { "test", "flag_alignment" } };

    [Fact]
    public void ValidateFlagFrameAlignment()
    {
        using var stream = new MemoryStream();
        using var writer = new Writer<FlagTestSession, FlagTestSessionFooter, FlagTestFrame>(stream, 60, TestMetadata);

        var testFrame = new FlagTestFrame
        {
            Flags1 = 0xF0F0F0F0F0F0F0F0,
            SmallValue = 0x42,
            Flags2 = 0xAAAAAAAA,
            SmallValue2 = 0x4242,
            Flags3 = 0x0F0F0F0F0F0F0F0F
        };

        writer.BeginSession(new FlagTestSession
        {
            SessionId = 1,
            SessionFlags = 0xCCCCCCCC,
            ExtendedFlags = 0x1111111111111111
        });

        writer.WriteFrame(1, testFrame);
        writer.EndSession(new FlagTestSessionFooter
        {
            FinalFlags = 0x2222222222222222,
            StatusFlags = 0x33333333
        });

        var bytes = stream.ToArray();
        var frameStart = FindFrameStart(bytes);
        var frameDataStart = frameStart + SpanReader.GetAlignedSize<FrameHeader>();

        // Verify frame data alignment
        Assert.True(frameDataStart % 8 == 0, $"Frame data not aligned at position {frameDataStart}");

        var readFrame = SpanReader.ReadAlignedStruct<FlagTestFrame>(
            new ReadOnlySpan<byte>(bytes, frameDataStart, SpanReader.GetAlignedSize<FlagTestFrame>()));

        // Verify all flag values are preserved
        Assert.Equal(0xF0F0F0F0F0F0F0F0UL, readFrame.Flags1);
        Assert.Equal(0x42, readFrame.SmallValue);
        Assert.Equal(0xAAAAAAAAU, readFrame.Flags2);
        Assert.Equal(0x4242, readFrame.SmallValue2);
        Assert.Equal(0x0F0F0F0F0F0F0F0FUL, readFrame.Flags3);
    }

    [Fact]
    public void ValidateFlagSessionAlignment()
    {
        using var stream = new MemoryStream();
        using var writer = new Writer<FlagTestSession, FlagTestSessionFooter, FlagTestFrame>(stream, 60, TestMetadata);

        var session = new FlagTestSession
        {
            SessionId = 1,
            SessionFlags = 0xCCCCCCCC,
            ExtendedFlags = 0x1111111111111111
        };

        writer.BeginSession(session);
        writer.EndSession(new FlagTestSessionFooter
        {
            FinalFlags = 0x2222222222222222,
            StatusFlags = 0x33333333
        });

        var bytes = stream.ToArray();
        var sessionStart = FindSessionStart(bytes);
        var sessionDataStart = sessionStart + Magic.MagicSize;

        // Verify session data alignment
        Assert.True(sessionDataStart % 8 == 0, $"Session data not aligned at position {sessionDataStart}");

        var readSession = SpanReader.ReadAlignedStruct<FlagTestSession>(
            new ReadOnlySpan<byte>(bytes, sessionDataStart, SpanReader.GetAlignedSize<FlagTestSession>()));

        // Verify all flag values are preserved
        Assert.Equal(1, readSession.SessionId);
        Assert.Equal(0xCCCCCCCCU, readSession.SessionFlags);
        Assert.Equal(0x1111111111111111UL, readSession.ExtendedFlags);
    }

    [Fact]
    public void ValidateFlagFooterAlignment()
    {
        using var stream = new MemoryStream();
        using var writer = new Writer<FlagTestSession, FlagTestSessionFooter, FlagTestFrame>(stream, 60, TestMetadata);

        writer.BeginSession(new FlagTestSession());

        var footer = new FlagTestSessionFooter
        {
            FinalFlags = 0x2222222222222222,
            StatusFlags = 0x33333333
        };

        writer.EndSession(footer);

        var bytes = stream.ToArray();
        var footerStart = FindFooterStart(bytes);
        var footerDataStart = footerStart + Magic.MagicSize + sizeof(ulong) * 2; // Magic + frame count + last tick

        // Verify footer data alignment
        Assert.True(footerDataStart % 8 == 0, $"Footer data not aligned at position {footerDataStart}");

        var readFooter = SpanReader.ReadAlignedStruct<FlagTestSessionFooter>(
            new ReadOnlySpan<byte>(bytes, footerDataStart, SpanReader.GetAlignedSize<FlagTestSessionFooter>()));

        // Verify all flag values are preserved
        Assert.Equal(0x2222222222222222UL, readFooter.FinalFlags);
        Assert.Equal(0x33333333U, readFooter.StatusFlags);
    }

    [Theory]
    [InlineData(0x0000000000000001UL)]  // Single bit set
    [InlineData(0x8000000000000000UL)]  // Highest bit set
    [InlineData(0xFFFFFFFFFFFFFFFFUL)]  // All bits set
    [InlineData(0x5555555555555555UL)]  // Alternating bits
    [InlineData(0xAAAAAAAAAAAAAAAAUL)]  // Alternating bits (inverse)
    public void ValidateFlagBitPatterns(ulong flagPattern)
    {
        using var stream = new MemoryStream();
        using var writer = new Writer<FlagTestSession, FlagTestSessionFooter, FlagTestFrame>(stream, 60, TestMetadata);

        var testFrame = new FlagTestFrame { Flags1 = flagPattern };

        writer.BeginSession(new FlagTestSession());
        writer.WriteFrame(1, testFrame);
        writer.EndSession(new FlagTestSessionFooter());

        var bytes = stream.ToArray();
        var frameStart = FindFrameStart(bytes);
        var frameDataStart = frameStart + SpanReader.GetAlignedSize<FrameHeader>();

        var readFrame = SpanReader.ReadAlignedStruct<FlagTestFrame>(
            new ReadOnlySpan<byte>(bytes, frameDataStart, SpanReader.GetAlignedSize<FlagTestFrame>()));

        Assert.Equal(flagPattern, readFrame.Flags1);
    }

    private static int FindSessionStart(byte[] bytes)
    {
        var sessionMagic = System.Text.Encoding.ASCII.GetBytes(Magic.SessionHeaderMagic);
        for (var i = 0; i <= bytes.Length - sessionMagic.Length; i++)
        {
            if (new ReadOnlySpan<byte>(bytes, i, sessionMagic.Length).SequenceEqual(sessionMagic))
                return i;
        }
        throw new InvalidOperationException("Session start not found");
    }

    private static int FindFrameStart(byte[] bytes)
    {
        var sessionStart = FindSessionStart(bytes);
        return sessionStart + Magic.MagicSize + SpanReader.GetAlignedSize<FlagTestSession>();
    }

    private static int FindFooterStart(byte[] bytes)
    {
        var footerMagic = System.Text.Encoding.ASCII.GetBytes(Magic.SessionFooterMagic);
        for (var i = 0; i <= bytes.Length - footerMagic.Length; i++)
        {
            if (new ReadOnlySpan<byte>(bytes, i, footerMagic.Length).SequenceEqual(footerMagic))
                return i;
        }
        throw new InvalidOperationException("Footer start not found");
    }
}
