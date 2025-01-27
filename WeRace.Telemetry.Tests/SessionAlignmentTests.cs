namespace WeRace.Telemetry.Tests;

public class SessionAlignmentTests
{
    [Fact]
    public void ValidateSessionAlignment()
    {
        using var stream = new MemoryStream();
        using var writer = new Writer<TestSession, TestFrame>(stream, 60);

        var session1 = new TestSession { SessionId = 1 };
        writer.BeginSession(session1);
        writer.WriteFrame(new TestFrame());
        writer.EndSession();

        var session2 = new TestSession { SessionId = 2 };
        writer.BeginSession(session2);
        writer.WriteFrame(new TestFrame());
        writer.EndSession();

        var bytes = stream.ToArray();
        AssertSessionBoundaries(bytes);
    }

    [Fact]
    public void ValidateSessionHeaderAndFooterPositioning()
    {
        using var stream = new MemoryStream();
        using var writer = new Writer<TestSession, TestFrame>(stream, 60);

        var session = new TestSession { SessionId = 1 };
        writer.BeginSession(session);

        for (int i = 0; i < 5; i++)
        {
            writer.WriteFrame(new TestFrame());
        }

        writer.EndSession();

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
        using var writer = new Writer<TestSession, TestFrame>(stream, 60);

        for (int i = 1; i <= 3; i++)
        {
            writer.BeginSession(new TestSession { SessionId = i });
            writer.WriteFrame(new TestFrame());
            writer.EndSession();
        }

        var bytes = stream.ToArray();
        var positions = FindAllSessionBoundaries(bytes);

        for (int i = 0; i < positions.Count - 1; i++)
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
      using var writer = new Writer<TestSession, TestFrame>(stream, 60);

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
        writer.WriteFrame(frame);
      }

      writer.EndSession();

      Debugging.DumpFileStructure(stream);

      stream.Position = 0;
      var reader = Reader<TestSession, TestFrame>.Open(stream);

      Assert.Single(reader.Sessions);
      var readSession = reader.Sessions[0];
      var frames = reader.GetFrames(readSession).ToList();

      Assert.Equal(5, frames.Count);
      for (var i = 0; i < 5; i++)
      {
        var frame = frames[i];
        Assert.Equal(i * 10.0f, frame.Data.Speed);
        Assert.Equal(i * 1000.0f, frame.Data.RPM);
      }
    }

    private static void AssertSessionBoundaries(byte[] bytes)
    {
        var positions = FindAllSessionBoundaries(bytes);

        foreach (var pos in positions)
        {
            Assert.True(SpanReader.TryReadMagic(
                new ReadOnlySpan<byte>(bytes, (int)pos.headerPos, 8),
                Magic.SessionMagic),
                $"Invalid session header magic at position {pos.headerPos}");

            Assert.True(SpanReader.TryReadMagic(
                new ReadOnlySpan<byte>(bytes, (int)pos.footerPos, 8),
                Magic.SessionFooterMagic),
                $"Invalid session footer magic at position {pos.footerPos}");

            Assert.True(pos.headerPos % 8 == 0, $"Header at {pos.headerPos} not aligned");
            Assert.True(pos.footerPos % 8 == 0, $"Footer at {pos.footerPos} not aligned");
        }
    }

    private static (long headerPos, long footerPos) FindSessionBoundaries(byte[] bytes)
    {
        var headerMagic = System.Text.Encoding.ASCII.GetBytes(Magic.SessionMagic);
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
        var headerMagic = System.Text.Encoding.ASCII.GetBytes(Magic.SessionMagic);
        var footerMagic = System.Text.Encoding.ASCII.GetBytes(Magic.SessionFooterMagic);

        var currentPos = 0;
        while (currentPos < bytes.Length)
        {
            var headerPos = FindPattern(new ReadOnlySpan<byte>(bytes, currentPos, bytes.Length - currentPos), headerMagic);
            if (headerPos < 0) break;

            headerPos += currentPos;
            currentPos = headerPos + 8;

            var footerPos = FindPattern(new ReadOnlySpan<byte>(bytes, currentPos, bytes.Length - currentPos), footerMagic);
            if (footerPos < 0) break;

            footerPos += currentPos;
            currentPos = footerPos + 8;

            boundaries.Add((headerPos, footerPos));
        }

        return boundaries;
    }

    private static int FindPattern(ReadOnlySpan<byte> data, ReadOnlySpan<byte> pattern)
    {
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            if (data.Slice(i, pattern.Length).SequenceEqual(pattern))
            {
                return i;
            }
        }
        return -1;
    }


}
