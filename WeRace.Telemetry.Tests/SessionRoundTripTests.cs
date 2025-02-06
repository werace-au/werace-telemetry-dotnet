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

    using (var writer = new Writer<TestSession, TestSessionFooter, TestFrame>(stream, 60))
    {
      writer.BeginSession(new TestSession { SessionId = 1 });

      for (var i = 0; i < frameCount; i++)
      {
        writer.WriteFrame((ulong)i, new TestFrame());
      }
      writer.EndSession(new TestSessionFooter());
    }

    stream.Position = 0;
    var reader = Reader<TestSession, TestSessionFooter, TestFrame>.Open(stream);
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

    using (var writer = new Writer<TestSession, TestSessionFooter, TestFrame>(stream, 60))
    {
      writer.BeginSession(new TestSession { SessionId = 1 });
      writer.EndSession(new TestSessionFooter());
    }

    stream.Position = 0;
    var reader = Reader<TestSession, TestSessionFooter, TestFrame>.Open(stream);
    var session = Assert.Single(reader.Sessions);

    Assert.Equal(0UL, session.FrameCount);
    Assert.Equal(0UL, session.LastFrameTick);
    Assert.Empty(reader.GetFrames(session));
  }
}
