using System.Runtime.InteropServices;

namespace WeRace.Telemetry.Tests;

public class SessionFooterTests
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    private struct TestSessionWithFooter
    {
        public int SessionId;
        public float InitialFuel;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    private struct TestSessionFooter
    {
        public float FuelUsed;
        public int LapsCompleted;
        public float BestLapTime;
    }

    [Theory]
    [InlineData(0)]  // Empty session
    [InlineData(1)]  // Single frame
    [InlineData(10)] // Multiple frames
    [InlineData(100)] // Many frames
    public void FrameCountsArePreservedInFooter(int frameCount)
    {
        using var stream = new MemoryStream();
        using var writer = new Writer<TestSessionWithFooter, TestFrame>(stream, 60);

        writer.BeginSession(new TestSessionWithFooter { SessionId = 1, InitialFuel = 100.0f });
        for (var i = 0; i < frameCount; i++)
        {
            writer.WriteFrame(new TestFrame());
        }
        writer.EndSession();

        stream.Position = 0;
        var reader = Reader<TestSessionWithFooter, TestFrame>.Open(stream);
        var session = Assert.Single(reader.Sessions);

        Assert.Equal((ulong)frameCount, session.FrameCount);
        Assert.Equal(frameCount > 0 ? (ulong)(frameCount - 1) : 0UL, session.LastFrameTick);
    }

    [Fact]
    public void SessionFooterOffsetsAreCorrect()
    {
        using var stream = new MemoryStream();
        using var writer = new Writer<TestSessionWithFooter, TestFrame>(stream, 60);

        // Write two sessions with different frame counts
        writer.BeginSession(new TestSessionWithFooter { SessionId = 1, InitialFuel = 100.0f });
        writer.WriteFrame(new TestFrame());
        writer.WriteFrame(new TestFrame());
        writer.EndSession();

        var session2Start = stream.Position;
        writer.BeginSession(new TestSessionWithFooter { SessionId = 2, InitialFuel = 50.0f });
        writer.WriteFrame(new TestFrame());
        writer.EndSession();

        stream.Position = 0;
        var reader = Reader<TestSessionWithFooter, TestFrame>.Open(stream);

        Assert.Equal(2, reader.Sessions.Count);

        // Verify first session
        Assert.Equal(2UL, reader.Sessions[0].FrameCount);
        Assert.Equal(1UL, reader.Sessions[0].LastFrameTick);

        // Verify second session
        Assert.Equal(1UL, reader.Sessions[1].FrameCount);
        Assert.Equal(0UL, reader.Sessions[1].LastFrameTick);
        Assert.Equal(session2Start, reader.Sessions[1].StartOffset);
    }

    [Fact]
    public void MultipleSessionsPreserveFooterValues()
    {
        using var stream = new MemoryStream();
        using var writer = new Writer<TestSessionWithFooter, TestFrame>(stream, 60);

        // First session with 2 frames
        writer.BeginSession(new TestSessionWithFooter { SessionId = 1, InitialFuel = 100.0f });
        writer.WriteFrame(new TestFrame());
        writer.WriteFrame(new TestFrame());
        writer.EndSession();

        // Second session with 1 frame
        writer.BeginSession(new TestSessionWithFooter { SessionId = 2, InitialFuel = 50.0f });
        writer.WriteFrame(new TestFrame());
        writer.EndSession();

        // Empty session
        writer.BeginSession(new TestSessionWithFooter { SessionId = 3, InitialFuel = 25.0f });
        writer.EndSession();

        stream.Position = 0;
        var reader = Reader<TestSessionWithFooter, TestFrame>.Open(stream);

        Assert.Equal(3, reader.Sessions.Count);

        // Verify session data is preserved
        Assert.Equal(1, reader.Sessions[0].Data.SessionId);
        Assert.Equal(100.0f, reader.Sessions[0].Data.InitialFuel);
        Assert.Equal(2UL, reader.Sessions[0].FrameCount);

        Assert.Equal(2, reader.Sessions[1].Data.SessionId);
        Assert.Equal(50.0f, reader.Sessions[1].Data.InitialFuel);
        Assert.Equal(1UL, reader.Sessions[1].FrameCount);

        Assert.Equal(3, reader.Sessions[2].Data.SessionId);
        Assert.Equal(25.0f, reader.Sessions[2].Data.InitialFuel);
        Assert.Equal(0UL, reader.Sessions[2].FrameCount);
    }

    [Fact]
    public void IncompleteSessionsAreHandledCorrectly()
    {
        using var stream = new MemoryStream();
        using var writer = new Writer<TestSessionWithFooter, TestFrame>(stream, 60);

        // Write a complete session
        writer.BeginSession(new TestSessionWithFooter { SessionId = 1, InitialFuel = 100.0f });
        writer.WriteFrame(new TestFrame());
        writer.WriteFrame(new TestFrame());
        writer.EndSession();

        // Write an incomplete session (no EndSession call)
        writer.BeginSession(new TestSessionWithFooter { SessionId = 2, InitialFuel = 50.0f });
        writer.WriteFrame(new TestFrame());
        // Intentionally not calling EndSession

        stream.Position = 0;
        var reader = Reader<TestSessionWithFooter, TestFrame>.Open(stream);

        // Both sessions should be discovered
        Assert.Equal(2, reader.Sessions.Count);

        // Verify complete session
        Assert.Equal(1, reader.Sessions[0].Data.SessionId);
        Assert.Equal(100.0f, reader.Sessions[0].Data.InitialFuel);
        Assert.Equal(2UL, reader.Sessions[0].FrameCount);

        // Verify incomplete session
        Assert.Equal(2, reader.Sessions[1].Data.SessionId);
        Assert.Equal(50.0f, reader.Sessions[1].Data.InitialFuel);
        Assert.Equal(1UL, reader.Sessions[1].FrameCount);
    }

    [Fact]
    public void ForwardScanningPreservesSessionValues()
    {
        using var stream = new MemoryStream();

        // Create sessions without document footer by not disposing the writer
        var writer = new Writer<TestSessionWithFooter, TestFrame>(stream, 60);

        // First session with varying frame data
        writer.BeginSession(new TestSessionWithFooter { SessionId = 1, InitialFuel = 100.0f });
        for (var i = 0; i < 3; i++)
        {
            writer.WriteFrame(new TestFrame
            {
                Speed = i * 10.0f,
                RPM = 1000.0f + i * 500.0f,
                Throttle = i * 0.25f
            });
        }
        writer.EndSession();

        // Second session with a footer but no frames
        writer.BeginSession(new TestSessionWithFooter { SessionId = 2, InitialFuel = 75.0f });
        writer.EndSession();

        // Third session with a mix of frame values
        writer.BeginSession(new TestSessionWithFooter { SessionId = 3, InitialFuel = 50.0f });
        writer.WriteFrame(new TestFrame { Speed = 120.0f, RPM = 3000.0f, Throttle = 1.0f });
        writer.WriteFrame(new TestFrame { Speed = 80.0f, RPM = 2000.0f, Throttle = 0.5f });
        writer.EndSession();

        // Fourth session that's incomplete (no footer)
        writer.BeginSession(new TestSessionWithFooter { SessionId = 4, InitialFuel = 25.0f });
        writer.WriteFrame(new TestFrame { Speed = 60.0f, RPM = 1500.0f, Throttle = 0.3f });
        // Intentionally not calling EndSession

        stream.Position = 0;
        var reader = Reader<TestSessionWithFooter, TestFrame>.Open(stream);

        // Verify all sessions were found
        Assert.Equal(4, reader.Sessions.Count);

        // Verify first session
        {
            var session = reader.Sessions[0];
            Assert.Equal(1, session.Data.SessionId);
            Assert.Equal(100.0f, session.Data.InitialFuel);
            Assert.Equal(3UL, session.FrameCount);

            var frames = reader.GetFrames(session).ToList();
            Assert.Equal(3, frames.Count);
            for (var i = 0; i < 3; i++)
            {
                Assert.Equal(i * 10.0f, frames[i].Data.Speed);
                Assert.Equal(1000.0f + i * 500.0f, frames[i].Data.RPM);
                Assert.Equal(i * 0.25f, frames[i].Data.Throttle);
            }
        }

        // Verify empty session
        {
            var session = reader.Sessions[1];
            Assert.Equal(2, session.Data.SessionId);
            Assert.Equal(75.0f, session.Data.InitialFuel);
            Assert.Equal(0UL, session.FrameCount);
            Assert.Empty(reader.GetFrames(session));
        }

        // Verify third session with footer
        {
            var session = reader.Sessions[2];
            Assert.Equal(3, session.Data.SessionId);
            Assert.Equal(50.0f, session.Data.InitialFuel);
            Assert.Equal(2UL, session.FrameCount);

            var frames = reader.GetFrames(session).ToList();
            Assert.Equal(2, frames.Count);
            Assert.Equal(120.0f, frames[0].Data.Speed);
            Assert.Equal(80.0f, frames[1].Data.Speed);
        }

        // Verify incomplete session
        {
            var session = reader.Sessions[3];
            Assert.Equal(4, session.Data.SessionId);
            Assert.Equal(25.0f, session.Data.InitialFuel);
            Assert.Equal(1UL, session.FrameCount);

            var frames = reader.GetFrames(session).ToList();
            Assert.Single(frames);
            Assert.Equal(60.0f, frames[0].Data.Speed);
            Assert.Equal(1500.0f, frames[0].Data.RPM);
            Assert.Equal(0.3f, frames[0].Data.Throttle);
        }
    }
}

