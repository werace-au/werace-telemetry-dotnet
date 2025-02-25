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
  title: "Sample Car Telemetry"
  description: "Complete telemetry channel set for a sample car"

session:
  description: "Session information"
  header:
    description: "Session initialization data"
    fields:
      - name: type
        type: session_type
        dimensions: 0
        description: The type of Session
        tags: ["metadata"]
      - name: driver_id
        type: uint32
        dimensions: 0
        description: "Driver identifier"
        tags: ["metadata"]
      - name: ambient_temp
        type: float32
        dimensions: 0
        description: "Ambient temperature at session start"
        unit: "celsius"
        tags: ["weather"]
  footer:
    description: "Session summary information"
    fields:
      - name: total_laps
        type: uint32
        dimensions: 0
        description: "Total laps completed"
        tags: ["summary"]
      - name: best_lap_time
        type: uint32
        dimensions: 0
        description: "Best lap time achieved"
        unit: "ms"
        tags: ["summary"]
      - name: total_distance
        type: float32
        dimensions: 0
        description: "Total distance covered"
        unit: "meters"
        tags: ["summary"]
      - name: fuel_used
        type: float32
        dimensions: 0
        description: "Total fuel consumed"
        unit: "liters"
        tags: ["summary"]

types:
  log_level:
    type: enum
    description: "Log severity levels"
    values:
      - name: Debug
        value: 0
        description: "Debug message"
      - name: Info
        value: 1
        description: "Information message"
      - name: Error
        value: 2
        description: "Error message"

  session_type:
    type: enum
    description: "Session type"
    values:
      - name: Practice
        value: 0
        description: "Practice session"
      - name: Qualifying
        value: 1
        description: "Qualifying session"
      - name: Race
        value: 2
        description: "Race session"

  vector3:
    type: struct
    description: "3D vector measurement"
    fields:
      - name: x
        type: float32
        description: "X component"
        unit: "meters"
      - name: y
        type: float32
        description: "Y component"
        unit: "meters"
      - name: z
        type: float32
        description: "Z component"
        unit: "meters"

  wheel_data:
    type: struct
    description: "Combined wheel sensor data"
    fields:
      - name: temperature
        type: float32
        description: "Tire surface temperature"
        unit: "celsius"
      - name: pressure
        type: float32
        description: "Tire pressure"
        unit: "kpa"
      - name: vertical_load
        type: float32
        description: "Vertical load on tire"
        unit: "newtons"
      - name: camber
        type: float32
        description: "Dynamic camber angle"
        unit: "degrees"
      - name: rotation_speed
        type: float32
        description: "Wheel rotation speed"
        unit: "rpm"

  engine_temps:
    type: struct
    description: "Engine temperature sensors"
    fields:
      - name: coolant_temps
        type: float32
        dimensions: 4
        description: "Coolant temperatures at different points"
        unit: "celsius"
      - name: oil_temp
        type: float32
        description: "Engine oil temperature"
        unit: "celsius"
      - name: intake_temp
        type: float32
        description: "Intake air temperature"
        unit: "celsius"

channels:
  - name: timestamp
    type: uint64
    description: "Microseconds since session start"
    unit: "us"
    tags: ["required"]

  - name: position
    type: vector3
    description: "Vehicle position in track coordinates"
    tags: ["position", "critical"]

  - name: velocity
    type: vector3
    description: "Vehicle velocity vector"
    unit: "meters_per_second"
    tags: ["velocity", "critical"]

  - name: acceleration
    type: vector3
    description: "Vehicle acceleration vector"
    unit: "meters_per_second_squared"
    tags: ["acceleration", "critical"]

  - name: orientation
    type: vector3
    description: "Vehicle orientation (roll, pitch, yaw)"
    unit: "degrees"
    tags: ["orientation"]

  - name: angular_velocity
    type: vector3
    description: "Angular velocity (roll, pitch, yaw rates)"
    unit: "degrees_per_second"
    tags: ["angular_rate"]

  - name: wheels
    type: wheel_data
    dimensions: 4
    description: "Data for all wheels [FL, FR, RL, RR]"
    tags: ["wheels", "critical"]

  - name: engine_speed
    type: uint16
    description: "Engine RPM"
    unit: "rpm"
    tags: ["engine", "critical", "display"]

  - name: engine_temperature
    type: engine_temps
    description: "Engine temperature sensor array"
    tags: ["engine", "temperature"]

  - name: throttle_position
    type: float32
    description: "Throttle pedal position"
    unit: "percent"
    tags: ["pedals", "display"]

  - name: brake_pressure
    type: float32
    description: "Brake system pressure"
    unit: "kpa"
    tags: ["brakes", "critical"]

  - name: steering_angle
    type: float32
    description: "Steering wheel angle"
    unit: "degrees"
    tags: ["steering", "display"]

  - name: gear
    type: uint8
    description: "Current gear (0=neutral)"
    tags: ["transmission", "display"]

  - name: lap_number
    type: uint16
    description: "Current lap number"
    tags: ["lap", "display"]

  - name: lap_time
    type: uint32
    description: "Current lap time"
    unit: "ms"
    tags: ["lap", "display"]

  - name: oil_pressure
    type: float32
    description: "Engine oil pressure"
    unit: "kpa"
    tags: ["engine", "critical"]

  - name: fuel_pressure
    type: float32
    description: "Fuel rail pressure"
    unit: "kpa"
    tags: ["fuel"]

  - name: fuel_level
    type: float32
    description: "Fuel tank level"
    unit: "liters"
    tags: ["fuel", "critical", "display"]
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
