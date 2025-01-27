using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace WeRace.Telemetry.Tests;

public class RoundTripTests {
  // Generators

  [Property(MaxTest = 100)]
  public Property WriteAndReadPreservesData() {
    return Prop.ForAll(
      Arb.From(Generators.TestDataGen),
      testData => {
        using var stream = new MemoryStream();
        using (var writer = new Writer<TestSession, TestFrame>(stream, 60)) {
          writer.BeginSession(testData.session);
          foreach (var frame in testData.frames) {
            writer.WriteFrame(frame);
          }

          writer.EndSession();
        }

        // Reset stream position and ensure data was written
        stream.Position = 0;
        Assert.True(stream.Length > 0, "No data was written to the stream");

        var reader = Reader<TestSession, TestFrame>.Open(stream);
        Assert.NotNull(reader);
        Assert.NotEmpty(reader.Sessions);

        var session = Assert.Single(reader.Sessions);
        Assert.Equal(testData.session.SessionId, session.Data.SessionId);
        Assert.Equal(testData.session.TrackId, session.Data.TrackId);
        Assert.Equal(testData.session.CarId, session.Data.CarId);

        var frames = reader.GetFrames(session).ToArray();
        Assert.Equal(testData.frames.Length, frames.Length);

        for (var i = 0; i < frames.Length; i++) {
          AssertFramesEqual(testData.frames[i], frames[i].Data);
        }

        return true;
      }).Label("Write and read preserves all data");
  }

  [Theory]
  [InlineData(1)]
  [InlineData(2)]
  [InlineData(10)]
  [InlineData(100)]
  public void WriteAndReadMultipleSessions(int sessionCount) {
    using var stream = new MemoryStream();
    using var writer = new Writer<TestSession, TestFrame>(stream, 60);

    var sessions = new List<(TestSession session, TestFrame[] frames)>();
    var rnd = new Random(42);

    for (var i = 0; i < sessionCount; i++) {
      var session = new TestSession {
        SessionId = i + 1,
        TrackId = rnd.Next(1, 1000),
        CarId = rnd.Next(1, 10000)
      };

      var frames = Enumerable.Range(0, rnd.Next(10, 50))
        .Select(_ => GenerateRandomFrame(rnd))
        .ToArray();

      writer.BeginSession(session);
      foreach (var frame in frames) {
        writer.WriteFrame(frame);
      }

      writer.EndSession();

      sessions.Add((session, frames));
    }

    Debugging.DumpFileStructure(stream);

    stream.Position = 0;
    var reader = Reader<TestSession, TestFrame>.Open(stream);

    Assert.Equal(sessionCount, reader.Sessions.Count);

    for (var i = 0; i < sessionCount; i++) {
      var expectedSession = sessions[i];
      var actualSession = reader.Sessions[i];

      Assert.Equal(expectedSession.session.SessionId, actualSession.Data.SessionId);
      Assert.Equal(expectedSession.session.TrackId, actualSession.Data.TrackId);
      Assert.Equal(expectedSession.session.CarId, actualSession.Data.CarId);

      var frames = reader.GetFrames(actualSession).ToArray();
      Assert.Equal(expectedSession.frames.Length, frames.Length);

      for (var j = 0; j < frames.Length; j++) {
        AssertFramesEqual(expectedSession.frames[j], frames[j].Data);
      }
    }
  }

  [Fact]
  public void DisposingWriterDuringSessionWritesFooter() {
    using var stream = new MemoryStream();
    var session = new TestSession { SessionId = 1, TrackId = 1, CarId = 1 };
    var frames = new[] { GenerateRandomFrame(new Random(42)) };

    using (var writer = new Writer<TestSession, TestFrame>(stream, 60)) {
      writer.BeginSession(session);
      writer.WriteFrame(frames[0]);
      // Let disposal handle session end
    }

    stream.Position = 0;
    var reader = Reader<TestSession, TestFrame>.Open(stream);

    Assert.Single(reader.Sessions);
    var readFrames = reader.GetFrames(reader.Sessions[0]).ToArray();
    Assert.Single(readFrames);
    AssertFramesEqual(frames[0], readFrames[0].Data);
  }

  [Property(MaxTest = 100)]
  public Property HeaderRoundTripPreservesValues() {
    return Prop.ForAll(
      Arb.From(Generators.MetadataGen),
      metadata => {
        using var stream = new MemoryStream();
        var expectedSampleRate = 60UL;

        using (var writer = new Writer<TestSession, TestFrame>(stream, expectedSampleRate, metadata)) {
          writer.BeginSession(new TestSession { SessionId = 1 });
          writer.EndSession();
        }

        stream.Position = 0;
        var reader = Reader<TestSession, TestFrame>.Open(stream);
        var readHeader = reader.Header;

        foreach (var (key, value) in metadata) {
          Assert.True(readHeader.Metadata.Entries.ContainsKey(key), $"Missing key: {key}");
          Assert.Equal(value, readHeader.Metadata.Entries[key]);
        }

        Assert.Equal(1UL, readHeader.Version);
        Assert.Equal(expectedSampleRate, readHeader.SampleRate);
        Assert.True(readHeader.StartTimestamp > 0);

        return true;
      }).Label("Header values are preserved through read/write cycle");
  }

  [Theory]
  [InlineData("Track名", "Value值")]
  [InlineData("🏎️", "🏁")]
  [InlineData("Track\u0305", "Value\u0306")]
  public void MetadataRoundTripPreservesSpecialCharacters(string key, string value) {
    using var stream = new MemoryStream();
    var metadata = new Dictionary<string, string> { { key, value } };

    using (var writer = new Writer<TestSession, TestFrame>(stream, 60, metadata)) {
      writer.BeginSession(new TestSession { SessionId = 1 });
      writer.EndSession();
    }

    stream.Position = 0;
    var reader = Reader<TestSession, TestFrame>.Open(stream);
    var readMetadata = reader.Header.Metadata;

    Assert.True(readMetadata.Entries.ContainsKey(key));
    Assert.Equal(value, readMetadata.Entries[key]);
  }

  private static TestFrame GenerateRandomFrame(Random rnd) => new() {
    Speed = (float)(rnd.NextDouble() * 400),
    RPM = (float)(rnd.NextDouble() * 20000),
    Throttle = (float)rnd.NextDouble(),
    Brake = (float)rnd.NextDouble(),
    Steering = (float)(rnd.NextDouble() * 2 - 1),
    Position = new Vector3 {
      X = (float)(rnd.NextDouble() * 2000 - 1000),
      Y = (float)(rnd.NextDouble() * 2000 - 1000),
      Z = (float)(rnd.NextDouble() * 2000 - 1000)
    },
    Rotation = new Vector3 {
      X = (float)(rnd.NextDouble() * 360),
      Y = (float)(rnd.NextDouble() * 360),
      Z = (float)(rnd.NextDouble() * 360)
    }
  };

  private static void AssertFramesEqual(TestFrame expected, TestFrame actual) {
    Assert.Equal(expected.Speed, actual.Speed, 3);
    Assert.Equal(expected.RPM, actual.RPM, 3);
    Assert.Equal(expected.Throttle, actual.Throttle, 3);
    Assert.Equal(expected.Brake, actual.Brake, 3);
    Assert.Equal(expected.Steering, actual.Steering, 3);

    AssertVector3Equal(expected.Position, actual.Position);
    AssertVector3Equal(expected.Rotation, actual.Rotation);
  }

  private static void AssertVector3Equal(Vector3 expected, Vector3 actual) {
    Assert.Equal(expected.X, actual.X, 3);
    Assert.Equal(expected.Y, actual.Y, 3);
    Assert.Equal(expected.Z, actual.Z, 3);
  }
}
