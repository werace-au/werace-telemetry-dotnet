using System;
using Xunit;

namespace WeRace.Telemetry.Tests;

public class SessionAlignmentTests
{
    [Fact]
    public void ValidateSessionAlignment()
    {
        using var stream = new MemoryStream();
        using var writer = new Writer<TestSession, TestSessionFooter, TestFrame>(stream, 60);

        var session1 = new TestSession { SessionId = 1 };
        writer.BeginSession(session1);
        writer.WriteFrame(100, new TestFrame());
        writer.EndSession(new TestSessionFooter());

        var session2 = new TestSession { SessionId = 2 };
        writer.BeginSession(session2);
        writer.WriteFrame(100, new TestFrame());
        writer.EndSession(new TestSessionFooter());

        var bytes = stream.ToArray();
        AssertSessionBoundaries(bytes);
    }

    [Fact]
    public void ValidateSessionHeaderAndFooterPositioning()
    {
        using var stream = new MemoryStream();
        using var writer = new Writer<TestSession, TestSessionFooter, TestFrame>(stream, 60);

        var session = new TestSession { SessionId = 1 };
        writer.BeginSession(session);

        for (var i = 0UL; i < 5; i++)
        {
            writer.WriteFrame(i, new TestFrame());
        }

        writer.EndSession(new TestSessionFooter());

        var bytes = stream.ToArray();
        var (headerPos, footerPos) = FindSessionBoundaries(bytes);

        Assert.True(headerPos >= 40); // After file header
        Assert.True(headerPos % 8 == 0, "Session header not aligned");
        Assert.True(footerPos % 8 == 0, "Session footer not aligned");

        var frameCount = (footerPos - headerPos - 8 - StructSizeHelper<TestSession>.Size) /
                        SizeCalculator.GetTotalFrameSize<TestFrame>(StructSizeHelper<FrameHeader>.Size);

        Assert.Equal(5, frameCount);
    }

    [Fact]
    public void ValidateMultipleSessionAlignment()
    {
        using var stream = new MemoryStream();
        using var writer = new Writer<TestSession, TestSessionFooter, TestFrame>(stream, 60);

        for (var i = 1; i <= 3; i++)
        {
            writer.BeginSession(new TestSession { SessionId = i });
            writer.WriteFrame(100, new TestFrame());
            writer.EndSession(new TestSessionFooter());
        }

        var bytes = stream.ToArray();
        var positions = FindAllSessionBoundaries(bytes);

        for (var i = 0; i < positions.Count - 1; i++)
        {
            Assert.True(positions[i].footerPos < positions[i + 1].headerPos,
                       $"Session {i + 1} overlaps with session {i + 2}");
            Assert.True((positions[i + 1].headerPos - positions[i].footerPos) % 8 == 0,
                       $"Gap between sessions {i + 1} and {i + 2} not aligned");
        }
    }

    [Fact]
    public void ValidateSessionFooterWriteRead()
    {
        using var stream = new MemoryStream();
        var writer = new Writer<TestSession, TestSessionFooter, TestFrame>(stream, 60);

        try
        {
            var session = new TestSession { SessionId = 1 };
            writer.BeginSession(session);

            for (var i = 0; i < 5; i++)
            {
                var frame = new TestFrame
                {
                    Speed = i * 10.0f,
                    RPM = i * 1000.0f,
                    Throttle = i * 0.2f,
                    Brake = i * 0.1f,
                    Steering = i * 0.25f,
                    Position = new Vector3 { X = i, Y = i * 2, Z = i * 3 },
                    Rotation = new Vector3 { X = i * 10, Y = i * 20, Z = i * 30 }
                };
                writer.WriteFrame((ulong) i, frame);
            }

            writer.EndSession(new TestSessionFooter { FuelUsed = 50.0f, LapsCompleted = 5, BestLapTime = 123.456f });
        }
        finally
        {
            writer.Dispose();
        }

        Debugging.DumpFileStructure(stream);

        stream.Position = 0;
        var reader = Reader<TestSession, TestSessionFooter, TestFrame>.Open(stream);
        var readSession = Assert.Single(reader.Sessions);

        Assert.Equal(5UL, readSession.FrameCount);
        Assert.Equal(4UL, readSession.LastFrameTick);
        Assert.Equal(50.0f, readSession.Footer.FuelUsed);
        Assert.Equal(5, readSession.Footer.LapsCompleted);
        Assert.Equal(123.456f, readSession.Footer.BestLapTime);
    }

    private static void AssertSessionBoundaries(byte[] bytes)
    {
        var positions = FindAllSessionBoundaries(bytes);
        foreach (var pos in positions)
        {
            Assert.True(pos.headerPos % 8 == 0, $"Header at {pos.headerPos} not aligned");
            Assert.True(pos.footerPos % 8 == 0, $"Footer at {pos.footerPos} not aligned");
        }
    }

    private static (long headerPos, long footerPos) FindSessionBoundaries(byte[] bytes)
    {
        var headerMagic = System.Text.Encoding.ASCII.GetBytes(Magic.SessionHeaderMagic);
        var footerMagic = System.Text.Encoding.ASCII.GetBytes(Magic.SessionFooterMagic);

        var headerPos = FindPattern(bytes, headerMagic);
        var footerPos = FindPattern(bytes, footerMagic);

        Assert.True(headerPos >= 0, "Session header not found");
        Assert.True(footerPos >= 0, "Session footer not found");

        return (headerPos, footerPos);
    }

    private static List<(long headerPos, long footerPos)> FindAllSessionBoundaries(byte[] bytes)
    {
        var boundaries = new List<(long headerPos, long footerPos)>();
        var headerMagic = System.Text.Encoding.ASCII.GetBytes(Magic.SessionHeaderMagic);
        var footerMagic = System.Text.Encoding.ASCII.GetBytes(Magic.SessionFooterMagic);

        var pos = 0;
        while (pos < bytes.Length)
        {
            var headerPos = FindPattern(bytes, headerMagic, pos);
            if (headerPos < 0) break;

            var footerPos = FindPattern(bytes, footerMagic, headerPos);
            if (footerPos < 0) break;

            boundaries.Add((headerPos, footerPos));
            pos = footerPos + footerMagic.Length;
        }

        return boundaries;
    }

    private static int FindPattern(byte[] data, byte[] pattern, int startIndex = 0)
    {
        for (var i = startIndex; i <= data.Length - pattern.Length; i++)
        {
            var found = true;
            for (var j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j])
                {
                    found = false;
                    break;
                }
            }
            if (found) return i;
        }
        return -1;
    }
}
