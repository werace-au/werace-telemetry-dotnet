# WeRace.Telemetry

Core library implementation for reading and writing WeRace Telemetry Format (WRTF) data. This project provides the fundamental components and APIs for handling telemetry data streams.

## Core Components

### Frame Handling

```csharp
public readonly record struct Frame<FRAME> where FRAME : struct
{
    public required FrameHeader Header { get; init; }
    public required FRAME Data { get; init; }
}
```

### Session Management

```csharp
public readonly record struct SessionInfo<SESSION> where SESSION : struct
{
    public required SESSION Data { get; init; }
    public required ulong FrameCount { get; init; }
    public required ulong LastFrameTick { get; init; }
    public required long StartOffset { get; init; }
    public required long DataOffset { get; init; }
}
```

## Key Features

- Binary telemetry data streaming with high performance
- Generic type-safe frame handling
- Session management and information tracking
- Built-in debugging utilities
- Memory-efficient struct-based data structures

## Usage Example

```csharp
using WeRace.Telemetry;

// Reading telemetry data
using var reader = new TelemetryReader<TFrame, TSession>(stream);
var sessionInfo = reader.ReadSessionInfo();

foreach (var frame in reader.ReadFrames())
{
    // Process frame.Header.TickCount and frame.Data
}
```

## File Format Magic Numbers

- File Header: `WRTF0001`
- Session Start: `WRSE0001`
- Session Footer: `WRSF0001`

## Dependencies

- .NET 8.0
