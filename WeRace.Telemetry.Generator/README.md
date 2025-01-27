# WeRace.Telemetry.Generator

A source generator for creating strongly-typed telemetry structures from YAML definitions. This project enables compile-time generation of telemetry data models for the WeRace Telemetry Format.

## Features

- Compile-time generation of telemetry data structures
- YAML-based telemetry definition parsing
- Automatic validation of telemetry definitions
- Generation of efficient struct-based models
- Support for complex nested types and arrays

## Usage

1. Create a YAML telemetry definition file in your project:

```yaml
version: "1.0"
metadata:
  title: "Race Car Telemetry"
  description: "Telemetry definition for race car data"

types:
  wheel_data:
    type: struct
    description: "Wheel sensor data"
    fields:
      - name: temperature
        type: float32
        description: "Tire surface temperature"
        unit: "celsius"
      - name: pressure
        type: float32
        description: "Tire pressure"
        unit: "kpa"

channels:
  - name: wheels
    type: wheel_data
    dimensions: 4
    description: "Data for all wheels [FL, FR, RL, RR]"
    tags: ["wheels", "critical"]
```

2. Add the generator to your project:

```xml
<ItemGroup>
    <ProjectReference Include="..\WeRace.Telemetry.Generator\WeRace.Telemetry.Generator.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
</ItemGroup>
```

3. The generator will create strongly-typed C# structures at compile time.

## Supported Types

- Basic Types: `uint8`, `uint16`, `uint32`, `uint64`, `float32`, `float64`
- Arrays: Fixed-size arrays of any supported type
- Structs: Custom struct definitions with nested fields
- Enums: Custom enumeration types

## Error Diagnostics

The generator provides detailed error diagnostics:

- TEL003: Code Generation Error
- TEL100: Type Generation Error
- TEL200: Reader Generation Error

## Dependencies

- .NET 8.0
- YamlDotNet for YAML parsing
