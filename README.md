# WeRace.Telemetry Solution

A comprehensive .NET solution for handling racing simulation telemetry data using the WeRace Telemetry Format (WRTF). This solution provides tools for reading, writing, and generating telemetry data structures with a focus on performance and type safety.

## Projects

### WeRace.Telemetry

Core library implementation providing the fundamental components for reading and writing telemetry data. See the [project README](WeRace.Telemetry/README.md) for detailed documentation.

### WeRace.Telemetry.Generator

Source generator for creating strongly-typed telemetry structures from YAML definitions. Enables compile-time generation of telemetry data models. See the [project README](WeRace.Telemetry.Generator/README.md) for usage instructions and supported features.

### WeRace.Telemetry.Tests

Test suite covering the core library and generator functionality.

## Installation

Install the core package via NuGet:

```bash
dotnet add package WeRace.Telemetry
```

## Features

- Fast and efficient binary telemetry data streaming
- YAML-based telemetry definition format
- Source generation for type-safe telemetry structures
- Support for complex telemetry data structures
- Compatible with racing simulators like iRacing and RaceRoom
- Built-in debugging and validation tools

## Telemetry Definition Example

```yaml
version: "1.0"
metadata:
  title: "WeRace Telemetry"
  description: "A telemetry definition for race data"

types:
  vector3:
    type: struct
    description: "3D vector measurement"
    fields:
      - name: x
        type: float32
        unit: "meters"
      - name: y
        type: float32
        unit: "meters"
      - name: z
        type: float32
        unit: "meters"
```

## Requirements

- .NET 8.0 or higher
- C# 12.0 or higher

## Building from Source

1. Clone the repository:

```bash
git clone https://github.com/werace-au/werace-telemetry-dotnet.git
```

1. Build the solution:

```bash
dotnet build
```

1. Run the tests:

```bash
dotnet test
```

## Documentation

- [Core Library Documentation](WeRace.Telemetry/README.md)
- [Format Specification](Documentation/wrtf-spec-v1.md)

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Authors

- Kevin O'Neill
