using System.Runtime.InteropServices;

namespace WeRace.Telemetry.Tests;

public class SessionFooterTests
{
  [StructLayout(LayoutKind.Sequential, Pack = 8)]
  private struct TestSessionHeader
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
    Writer<TestSessionHeader, TestSessionFooter, TestFrame>? writer = null;

    try
    {
      writer = new Writer<TestSessionHeader, TestSessionFooter, TestFrame>(stream, 60);

      writer.BeginSession(new TestSessionHeader { SessionId = 1, InitialFuel = 100.0f });
      for (var i = 0; i < frameCount; i++)
      {
        writer.WriteFrame((ulong)i, new TestFrame());
      }
      writer.EndSession(new TestSessionFooter { FuelUsed = 50.0f, LapsCompleted = frameCount / 10, BestLapTime = 123.456f });
    }
    finally
    {
      writer?.Dispose();
    }

    stream.Position = 0;
    var reader = Reader<TestSessionHeader, TestSessionFooter, TestFrame>.Open(stream);
    var readSession = Assert.Single(reader.Sessions);

    Assert.Equal((ulong)frameCount, readSession.FrameCount);
    Assert.Equal(frameCount > 0 ? (ulong)(frameCount - 1) : 0UL, readSession.LastFrameTick);
    Assert.Equal(50.0f, readSession.Footer.FuelUsed);
    Assert.Equal(frameCount / 10, readSession.Footer.LapsCompleted);
    Assert.Equal(123.456f, readSession.Footer.BestLapTime);
  }

  [Fact]
  public void SessionFooterOffsetsAreCorrect()
  {
    using var stream = new MemoryStream();
    var writer = new Writer<TestSessionHeader, TestSessionFooter, TestFrame>(stream, 60);
    long session2Start;

    try
    {
      // Write two sessions with different frame counts
      writer.BeginSession(new TestSessionHeader { SessionId = 1, InitialFuel = 100.0f });
      writer.WriteFrame(0UL, new TestFrame());
      writer.WriteFrame(1UL, new TestFrame());
      writer.EndSession(new TestSessionFooter { FuelUsed = 25.0f, LapsCompleted = 1, BestLapTime = 60.0f });

      session2Start = stream.Position;
      writer.BeginSession(new TestSessionHeader { SessionId = 2, InitialFuel = 50.0f });
      writer.WriteFrame(0UL, new TestFrame());
      writer.EndSession(new TestSessionFooter { FuelUsed = 10.0f, LapsCompleted = 1, BestLapTime = 45.0f });
    }
    finally
    {
      writer.Dispose();
    }

    Debugging.DumpFileStructure(stream);

    stream.Position = 0;
    var reader = Reader<TestSessionHeader, TestSessionFooter, TestFrame>.Open(stream);

    Assert.Equal(2, reader.Sessions.Count);

    // Verify first session
    Assert.Equal(2UL, reader.Sessions[0].FrameCount);
    Assert.Equal(1UL, reader.Sessions[0].LastFrameTick);
    Assert.Equal(25.0f, reader.Sessions[0].Footer.FuelUsed);

    // Verify second session
    Assert.Equal(1UL, reader.Sessions[1].FrameCount);
    Assert.Equal(0UL, reader.Sessions[1].LastFrameTick);
    Assert.Equal(session2Start, reader.Sessions[1].StartOffset);
    Assert.Equal(10.0f, reader.Sessions[1].Footer.FuelUsed);
  }

  [Fact]
  public void MultipleSessionsPreserveFooterValues()
  {
    using var stream = new MemoryStream();
    Writer<TestSessionHeader, TestSessionFooter, TestFrame>? writer = null;

    try
    {
      writer = new Writer<TestSessionHeader, TestSessionFooter, TestFrame>(stream, 60);

      // First session with 2 frames
      writer.BeginSession(new TestSessionHeader { SessionId = 1, InitialFuel = 100.0f });
      writer.WriteFrame(0UL, new TestFrame());
      writer.WriteFrame(1UL, new TestFrame());
      writer.EndSession(new TestSessionFooter { FuelUsed = 25.0f, LapsCompleted = 2, BestLapTime = 60.0f });

      // Second session with 1 frame
      writer.BeginSession(new TestSessionHeader { SessionId = 2, InitialFuel = 50.0f });
      writer.WriteFrame(0UL, new TestFrame());
      writer.EndSession(new TestSessionFooter { FuelUsed = 10.0f, LapsCompleted = 1, BestLapTime = 45.0f });

      // Empty session
      writer.BeginSession(new TestSessionHeader { SessionId = 3, InitialFuel = 25.0f });
      writer.EndSession(new TestSessionFooter { FuelUsed = 0.0f, LapsCompleted = 0, BestLapTime = 0.0f });
    }
    finally
    {
      writer?.Dispose();
    }

    Debugging.DumpFileStructure(stream);

    stream.Position = 0;
    var reader = Reader<TestSessionHeader, TestSessionFooter, TestFrame>.Open(stream);

    Assert.Equal(3, reader.Sessions.Count);

    // Verify session data is preserved
    Assert.Equal(1, reader.Sessions[0].Header.SessionId);
    Assert.Equal(100.0f, reader.Sessions[0].Header.InitialFuel);
    Assert.Equal(2UL, reader.Sessions[0].FrameCount);
    Assert.Equal(25.0f, reader.Sessions[0].Footer.FuelUsed);
    Assert.Equal(2, reader.Sessions[0].Footer.LapsCompleted);
    Assert.Equal(60.0f, reader.Sessions[0].Footer.BestLapTime);

    Assert.Equal(2, reader.Sessions[1].Header.SessionId);
    Assert.Equal(50.0f, reader.Sessions[1].Header.InitialFuel);
    Assert.Equal(1UL, reader.Sessions[1].FrameCount);
    Assert.Equal(10.0f, reader.Sessions[1].Footer.FuelUsed);
    Assert.Equal(1, reader.Sessions[1].Footer.LapsCompleted);
    Assert.Equal(45.0f, reader.Sessions[1].Footer.BestLapTime);

    Assert.Equal(3, reader.Sessions[2].Header.SessionId);
    Assert.Equal(25.0f, reader.Sessions[2].Header.InitialFuel);
    Assert.Equal(0UL, reader.Sessions[2].FrameCount);
    Assert.Equal(0.0f, reader.Sessions[2].Footer.FuelUsed);
    Assert.Equal(0, reader.Sessions[2].Footer.LapsCompleted);
    Assert.Equal(0.0f, reader.Sessions[2].Footer.BestLapTime);
  }



}

