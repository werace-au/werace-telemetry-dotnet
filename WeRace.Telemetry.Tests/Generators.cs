using FsCheck;
using FsCheck.Fluent;

namespace WeRace.Telemetry.Tests;

internal static class Generators {
  public static Gen<TestSession> SessionGen =>
    from sessionId in Gen.Choose(1, int.MaxValue)
    from trackId in Gen.Choose(1, 1000)
    from carId in Gen.Choose(1, 10000)
    select new TestSession { SessionId = sessionId, TrackId = trackId, CarId = carId };

  public static Gen<float> FloatGen(float min, float max) =>
    from d in Gen.Choose(0, 1000000)
    select min + (max - min) * (d / 1000000f);

  public static Gen<Vector3> Vector3Gen =>
    from x in FloatGen(-1000f, 1000f)
    from y in FloatGen(-1000f, 1000f)
    from z in FloatGen(-1000f, 1000f)
    select new Vector3 { X = x, Y = y, Z = z };

  public static Gen<TestFrame> FrameGen =>
    from speed in FloatGen(0f, 400f)
    from rpm in FloatGen(0f, 20000f)
    from throttle in FloatGen(0f, 1f)
    from brake in FloatGen(0f, 1f)
    from steering in FloatGen(-1f, 1f)
    from pos in Vector3Gen
    from rot in Vector3Gen
    select new TestFrame {
      Speed = speed,
      RPM = rpm,
      Throttle = throttle,
      Brake = brake,
      Steering = steering,
      Position = pos,
      Rotation = rot
    };

  public static Gen<(TestSession session, TestFrame[] frames)> TestDataGen =>
    from session in SessionGen
    let frameGen = Gen.Choose(1, 100).SelectMany(count =>
      FrameGen.ArrayOf(count))
    from frames in frameGen
    select (session, frames);

  private static readonly Gen<(string key, string value)> MetadataEntryGen =
    from key in Gen.Elements("Track", "Car", "Driver", "Series", "Event")
      .SelectMany(k => Gen.Choose(1, 100).Select(n => $"{k}{n}"))
    from value in Gen.Elements("Value", "Test", "Sample")
      .SelectMany(v => Gen.Choose(1, 100).Select(n => $"{v}{n}"))
    select (key, value);

  public static readonly Gen<Dictionary<string, string>> MetadataGen =
    Gen.Choose(1, 10)
      .SelectMany(count => MetadataEntryGen.ListOf(count))
      .Select(list => list
        .GroupBy(x => x.key)
        .ToDictionary(g => g.Key, g => g.First().value));
}
