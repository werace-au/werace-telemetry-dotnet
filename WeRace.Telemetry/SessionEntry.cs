namespace WeRace.Telemetry;

/// <summary>
/// Represents a session entry in the document footer.
/// </summary>
public readonly struct SessionEntry
{
    /// <summary>
    /// The byte offset in the stream where the session starts.
    /// </summary>
    public required long SessionOffset { get; init; }

    /// <summary>
    /// The byte offset in the stream where the session footer starts.
    /// </summary>
    public required long FooterOffset { get; init; }

    /// <summary>
    /// The number of frames in the session.
    /// </summary>
    public required ulong FrameCount { get; init; }
}
