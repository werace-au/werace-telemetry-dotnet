# WRTF Channel Definition Schema

This document defines the schema for WRTF channel and session definitions.

## Structure

The schema consists of four main sections:
- Version information
- Metadata
- Type definitions
- Channel definitions

## Schema Definition

```yaml
version: "1.0"

metadata:
  # Name of the channel set
  title: string
  # Description of the channel set
  description: string

# Optional common type definitions that can be referenced
types:
  # Custom type identifier
  type_name:
    # Base type - one of:
    # struct or enum
    type: string
    # Description of the type
    description: string

    # For enum types only:
    values:
      - # Value name
        name: string
        # Unsigned integer value
        value: uint
        # Description of the value
        description: string

    # For struct types only:
    fields:
      - # Field name
        name: string
        # Field type (primitive or custom type name)
        type: string
        # Optional array size (default: 0)
        dimensions: number
        # Description of the field
        description: string
        # Optional unit
        unit: string
        # Optional tags for categorization
        tags: [string]

# Session type definition (required)
session:
  type: struct
  description: "Session information structure"
  fields:
    - # Field name
      name: string
      # Field type (primitive or custom type name)
      type: string
      # Optional array size (default: 0)
      dimensions: number
      # Description of the field
      description: string
      # Optional unit
      unit: string
      # Optional tags for categorization
      tags: [string]

# Channel definitions
channels:
  - # Channel name
    name: string
    # Type (primitive or custom type name)
    type: string
    # Optional array size (default: 0)
    dimensions: number
    # Description of the channel
    description: string
    # Optional unit
    unit: string
    # Optional tags for categorization
    tags: [string]
```

## Example Usage

```yaml
types:
  gear_state:
    type: enum
    description: "Transmission gear state"
    values:
      - name: neutral
        value: 0
        description: "Transmission in neutral"
      - name: first
        value: 1
        description: "First gear"
      - name: reverse
        value: 7
        description: "Reverse gear"

  wheel_data:
    type: struct
    description: "Combined wheel sensor data"
    fields:
      - name: temperature
        type: float32
        dimensions: 0
        description: "Tire surface temperature"
        unit: "celsius"
        tags: ["temperature", "critical"]

      - name: pressure
        type: float32
        dimensions: 0
        description: "Tire pressure"
        unit: "kpa"
        tags: ["pressure", "critical"]

session:
  type: struct
  description: "Session information"
  fields:
    - name: type
      type: uint32
      dimensions: 0
      description: "Session type identifier"
      tags: ["metadata"]

    - name: driver_id
      type: uint32
      dimensions: 0
      description: "Driver identifier"
      tags: ["metadata"]

channels:
  - name: wheels
    type: wheel_data
    dimensions: 4
    description: "Data for all wheels [FL, FR, RL, RR]"
    tags: ["wheels", "critical"]

  - name: current_gear
    type: gear_state
    dimensions: 0
    description: "Current transmission gear"
    tags: ["transmission", "display"]
```

## Validation Rules

### Type-Specific Rules

#### Enum Types
- Must have at least one value
- All values must have unique names within the enum
- All values must have unique numeric values
- Cannot have fields defined
- All value names and descriptions must be non-empty strings

#### Struct Types
- Must have at least one field
- All fields must have valid types (primitive, enum, or struct)
- All field names and descriptions must be non-empty strings
- All field dimensions must be non-negative

### Session Rules
- Session type must be defined as a struct type
- Session fields must follow struct field rules

### General Rules
- All names and descriptions must be non-empty strings
- All dimensions must be non-negative
- No circular references allowed in custom types
- Channel types must reference valid primitive or custom types
- Version must be "1.0"

## Base Types
The following base types are supported:

- Integers: `int8`, `uint8`, `int16`, `uint16`, `int32`, `uint32`, `int64`, `uint64`
- Floating Point: `float32`, `float64`
- Boolean: `bool`
- Complex: `struct`, `enum`
