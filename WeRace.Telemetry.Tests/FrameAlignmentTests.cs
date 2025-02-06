using System;
using Xunit;
using Xunit.Abstractions;

namespace WeRace.Telemetry.Tests;

public class FrameAlignmentTests(ITestOutputHelper testOutputHelper) {
  [Fact]
  public void ValidateFrameHeaderAlignment() {
    using var stream = new MemoryStream();
    using (var writer = new Writer<TestSession, TestSessionFooter, TestFrame>(stream, 60)) {
      writer.BeginSession(new TestSession { SessionId = 1 });
      writer.WriteFrame(1023, new TestFrame());
      writer.EndSession(new TestSessionFooter());
    }

    Debugging.DumpFileStructure(stream);

    var bytes = stream.ToArray();
    var sessionStart = FindSessionStart(bytes);
    var frameStart = sessionStart + TelemetrySizeHelper<TestSession, TestSessionFooter, TestFrame>.TotalSessionHeaderSize;

    Assert.True(frameStart % 8 == 0, $"Frame start not aligned at position {frameStart}");
    var frameHeader  = SpanReader.ReadAlignedStruct<FrameHeader>(new(bytes, frameStart, TelemetrySizeHelper<TestSession, TestSessionFooter, TestFrame>.FrameHeaderSize));
    Assert.Equal(1023UL, frameHeader.TickCount);
  }

  [Fact]
  public void ValidateSequentialFrameAlignment() {
    using var stream = new MemoryStream();
    using var writer = new Writer<TestSession, TestSessionFooter, TestFrame>(stream, 60);

    writer.BeginSession(new TestSession { SessionId = 1 });

    // Write frames and print their expected tick counts
    const int frameCount = 3;
    for (var i = (ulong)0; i < frameCount; i++) {
      writer.WriteFrame(i, new TestFrame());
    }

    writer.EndSession(new TestSessionFooter());

    var bytes = stream.ToArray();
    var frameStarts = FindAllFrameStarts(bytes);

    Assert.Equal(frameCount, frameStarts.Count);

    for (var i = 0; i < frameStarts.Count; i++) {
      Assert.True(frameStarts[i] % 8 == 0, $"Frame {i} not aligned at position {frameStarts[i]}");

      var headerSpan = new ReadOnlySpan<byte>(bytes, frameStarts[i], StructSizeHelper<FrameHeader>.Size);
      var header = SpanReader.ReadAlignedStruct<FrameHeader>(headerSpan);

      // Print actual tick count for debugging
      testOutputHelper.WriteLine($"Frame {i}: Expected tick={i}, Actual tick={header.TickCount}");
      Assert.Equal((ulong)i, header.TickCount);
    }
  }

  [Fact]
  public void ValidateFrameDataAlignment() {
    using var stream = new MemoryStream();
    using var writer = new Writer<TestSession, TestSessionFooter, TestFrame>(stream, 60);

    var testFrame = new TestFrame {
      Speed = 100.0f,
      RPM = 5000.0f,
      Position = new Vector3 { X = 1, Y = 2, Z = 3 }
    };

    writer.BeginSession(new TestSession { SessionId = 1 });
    writer.WriteFrame(1, testFrame);
    writer.EndSession(new TestSessionFooter());

    var bytes = stream.ToArray();
    var frameStart = FindAllFrameStarts(bytes)[0];
    var headerSize = SpanReader.GetAlignedSize<FrameHeader>();
    var frameDataStart = frameStart + headerSize;

    Assert.True(frameDataStart % 8 == 0, $"Frame data not aligned at position {frameDataStart}");

    var readFrame = SpanReader.ReadAlignedStruct<TestFrame>(
      new ReadOnlySpan<byte>(bytes, frameDataStart, SpanReader.GetAlignedSize<TestFrame>()));

    Assert.Equal(testFrame.Speed, readFrame.Speed);
    Assert.Equal(testFrame.RPM, readFrame.RPM);
    Assert.Equal(testFrame.Position.X, readFrame.Position.X);
    Assert.Equal(testFrame.Position.Y, readFrame.Position.Y);
    Assert.Equal(testFrame.Position.Z, readFrame.Position.Z);
  }

  private static int FindSessionStart(byte[] bytes) {
    var sessionMagic = System.Text.Encoding.ASCII.GetBytes(Magic.SessionHeaderMagic);
    for (var i = 0; i <= bytes.Length - sessionMagic.Length; i++) {
      if (new ReadOnlySpan<byte>(bytes, i, sessionMagic.Length).SequenceEqual(sessionMagic))
        return i;
    }

    throw new InvalidOperationException("Session start not found");
  }

  private static List<int> FindAllFrameStarts(byte[] bytes) {
    var frameStarts = new List<int>();
    var sessionStart = FindSessionStart(bytes);
    var footerMagic = System.Text.Encoding.ASCII.GetBytes(Magic.SessionFooterMagic);

    var currentPos = sessionStart + 8 + StructSizeHelper<TestSession>.Size +
                     SpanReader.GetPadding(8 + StructSizeHelper<TestSession>.Size);

    while (currentPos + StructSizeHelper<FrameHeader>.Size <= bytes.Length) {
      // Check if we've hit the session footer
      if (currentPos + footerMagic.Length <= bytes.Length) {
        var possibleFooter = new ReadOnlySpan<byte>(bytes, currentPos, footerMagic.Length);
        if (possibleFooter.SequenceEqual(footerMagic))
          break;
      }

      frameStarts.Add(currentPos);
      currentPos += SizeCalculator.GetTotalFrameSize<TestFrame>(StructSizeHelper<FrameHeader>.Size);
    }

    return frameStarts;
  }
}
