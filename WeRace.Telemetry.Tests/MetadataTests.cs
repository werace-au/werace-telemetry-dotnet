using System.Text;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace WeRace.Telemetry.Tests;

public class MetadataTests {
  [Theory]
  [InlineData("key", "value")] // Basic case
  [InlineData("key", "value with spaces")] // Value with spaces
  [InlineData("long_key_name", "short")] // Long key, short value
  [InlineData("k", "very_long_value_that_needs_padding")] // Short key, long value
  [InlineData("key1", "")] // Empty value
  public void MetadataSizeCalculationIsCorrect(string key, string value) {
    using var stream = new MemoryStream();
    var metadata = new Dictionary<string, string> { { key, value } };
    using (var writer = new Writer<TestSession, TestSessionFooter, TestFrame>(stream, 60, metadata)) {

      // Write a minimal session to ensure metadata is written
      writer.BeginSession(new TestSession { SessionId = 1 });
      writer.EndSession(new TestSessionFooter());
    }

    Debugging.DumpFileStructure(stream);

    // Read back and verify
    stream.Position = 0;
    var reader = Reader<TestSession, TestSessionFooter, TestFrame>.Open(stream);

    Assert.Single(reader.Header.Metadata.Entries);
    Assert.Equal(value, reader.Header.Metadata.Entries[key]);
  }

  [Fact]
  public void MetadataWithMultipleEntriesPreservesAlignment() {
    using var stream = new MemoryStream();
    var metadata = new Dictionary<string, string> {
      { "key1", "value1" },
      { "key2", "longer_value_2" },
      { "longer_key_3", "v3" },
      { "k4", "v4" }
    };

    using (var writer = new Writer<TestSession, TestSessionFooter, TestFrame>(stream, 60, metadata)) {
      writer.BeginSession(new TestSession { SessionId = 1 });
      writer.EndSession(new TestSessionFooter());
    }

    Debugging.DumpFileStructure(stream);


    // Read back and verify
    stream.Position = 0;
    var reader = Reader<TestSession, TestSessionFooter, TestFrame>.Open(stream);

    Assert.Equal(4, reader.Header.Metadata.Entries.Count);
    foreach (var (key, value) in metadata) {
      Assert.Equal(value, reader.Header.Metadata.Entries[key]);
    }
  }

  [Fact]
  public void MetadataWithUnicodePreservesSize() {
    using var stream = new MemoryStream();
    var metadata = new Dictionary<string, string> {
      { "track", "日本サーキット" }, // Japanese characters
      { "driver", "名前" }, // More Japanese
      { "emoji", "🏎️🏁" }, // Emojis
      { "combined", "Race at 日本" } // Mixed ASCII and Unicode
    };

    using (var writer = new Writer<TestSession, TestSessionFooter, TestFrame>(stream, 60, metadata)) {
      writer.BeginSession(new TestSession { SessionId = 1 });
      writer.EndSession(new TestSessionFooter());
    }

    stream.Position = 0;
    var reader = Reader<TestSession, TestSessionFooter, TestFrame>.Open(stream);

    Assert.Equal(4, reader.Header.Metadata.Entries.Count);
    foreach (var (key, value) in metadata) {
      Assert.Equal(value, reader.Header.Metadata.Entries[key]);
    }
  }

  [Fact]
  public void LargeMetadataPreservesAlignment() {
    using var stream = new MemoryStream();
    var metadata = new Dictionary<string, string>();

    // Add 100 entries with varying sizes
    for (var i = 0; i < 100; i++) {
      var keyLength = (i % 3) + 1; // 1-3 characters
      var valueLength = (i % 5) + 1; // 1-5 characters
      var key = new string('k', keyLength) + i;
      var value = new string('v', valueLength) + i;
      metadata.Add(key, value);
    }

    using (var writer = new Writer<TestSession, TestSessionFooter, TestFrame>(stream, 60, metadata)) {
      writer.BeginSession(new TestSession { SessionId = 1 });
      writer.EndSession(new TestSessionFooter());
    }

    // Verify session starts on 8-byte boundary
    var sessionStart = stream.Position;
    Assert.Equal(0, sessionStart % 8);

    // Read back and verify
    stream.Position = 0;
    var reader = Reader<TestSession, TestSessionFooter, TestFrame>.Open(stream);

    Assert.Equal(100, reader.Header.Metadata.Entries.Count);
    foreach (var (key, value) in metadata) {
      Assert.Equal(value, reader.Header.Metadata.Entries[key]);
    }
  }

  [Fact]
  public void MetadataWithLargeSizes() {
    using var stream = new MemoryStream();

    // Create large strings that will need significant padding
    var largeKey1 = new string('k', 1000); // 100 character key
    var largeKey2 = new string('m', 1000); // Different 1000 character key
    var largeValue = new string('v', 10000); // 10000 character value

    var metadata = new Dictionary<string, string> {
      { largeKey1, "small" }, // Large key, small value
      { "small", largeValue }, // Small key, large value
      { largeKey2, largeValue } // Different large key, large value
    };

    using (var writer = new Writer<TestSession, TestSessionFooter, TestFrame>(stream, 60, metadata)) {
      writer.BeginSession(new TestSession { SessionId = 1 });
      writer.EndSession(new TestSessionFooter());
    }

    // Verify session starts on 8-byte boundary
    var sessionHeaderStart = stream.Position - Magic.MagicSize;
    Assert.True(sessionHeaderStart % 8 == 0, "Session header should be 8-byte aligned");

    // Read back and verify
    stream.Position = 0;
    var reader = Reader<TestSession, TestSessionFooter, TestFrame>.Open(stream);

    Assert.Equal(3, reader.Header.Metadata.Entries.Count);
    foreach (var (key, value) in metadata) {
      Assert.Equal(value, reader.Header.Metadata.Entries[key]);
    }
  }

  [Fact]
  public void EmptyMetadataPreservesAlignment() {
    using var stream = new MemoryStream();
    using (var writer = new Writer<TestSession, TestSessionFooter, TestFrame>(stream, 60)) {
      // Write a minimal session to ensure metadata is written
      writer.BeginSession(new TestSession { SessionId = 1 });
      writer.EndSession(new TestSessionFooter());
    }

    // At this point, the header and metadata have been written
    // and the session header has just been written
    var sessionHeaderStart = stream.Position - Magic.MagicSize; // Subtract magic number size
    Assert.True(sessionHeaderStart % 8 == 0, "Session header should be 8-byte aligned");
    Assert.True(sessionHeaderStart >= 40, "Session header should start after fixed header");

    // Read back and verify metadata
    stream.Position = 0;
    var reader = Reader<TestSession, TestSessionFooter, TestFrame>.Open(stream);
    Assert.Empty(reader.Header.Metadata.Entries);

    // Verify fixed header fields
    Assert.Equal(1UL, reader.Header.Version);
    Assert.Equal(60UL, reader.Header.SampleRate);
    Assert.True(reader.Header.StartTimestamp > 0);
  }
}
