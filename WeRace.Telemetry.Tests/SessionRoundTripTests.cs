namespace WeRace.Telemetry.Tests;

public class SessionRoundTripTests
{
  [Theory]
  [InlineData(1)]
  [InlineData(5)]
  [InlineData(100)]
  public void SessionPreservesFrameTickCounts(int frameCount)
  {
    using var stream = new MemoryStream();

    using (var writer = new Writer<TestSession, TestFrame>(stream, 60))
    {
      writer.BeginSession(new TestSession { SessionId = 1 });

      for (var i = 0; i < frameCount; i++)
      {
        writer.WriteFrame(new TestFrame());
      }
      writer.EndSession();
    }

    stream.Position = 0;
    var reader = Reader<TestSession, TestFrame>.Open(stream);
    var session = Assert.Single(reader.Sessions);

    Assert.Equal((ulong)frameCount, session.FrameCount);
    Assert.Equal((ulong)(frameCount - 1), session.LastFrameTick);

    var frames = reader.GetFrames(session).ToList();
    Assert.Equal(frameCount, frames.Count);

    for (var i = 0; i < frameCount; i++)
    {
      Assert.Equal((ulong)i, frames[i].Header.TickCount);
    }
  }

  [Fact]
  public void EmptySessionHasZeroLastFrameTick()
  {
    using var stream = new MemoryStream();

    using (var writer = new Writer<TestSession, TestFrame>(stream, 60))
    {
      writer.BeginSession(new TestSession { SessionId = 1 });
      writer.EndSession();
    }

    stream.Position = 0;
    var reader = Reader<TestSession, TestFrame>.Open(stream);
    var session = Assert.Single(reader.Sessions);

    Assert.Equal(0UL, session.FrameCount);
    Assert.Equal(0UL, session.LastFrameTick);
  }
}
