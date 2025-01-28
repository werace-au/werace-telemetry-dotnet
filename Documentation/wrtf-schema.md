# WRTF Channel Definition Schema

This document defines the schema for WRTF frame and session definitions.

## Structure

The schema consists of four main sections:

* Version information
* Metadata
* Type definitions
* Frame definition

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

# Session definition
session:
  # Description of the session
  description: string

  # Session header definition (required)
  header:
    # Description of the session header data
    description: string
    # Session header field definitions
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

  # Session footer definition (optional)
  footer:
    # Description of the session footer data
    description: string
    # Session footer field definitions
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

# Frame definition
frame:
  # Description of the frame data
  description: string
  # Frame field definitions
  fields:
    - # Field name
      name: string
      # Type (primitive or custom type name)
      type: string
      # Optional array size (default: 0)
      dimensions: number
      # Description of the field
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
  description: "Race session data"

  header:
    description: "Session initialization data"
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

  footer:
    description: "Session summary information"
    fields:
      - name: best_lap_time
        type: uint32
        dimensions: 0
        description: "Best lap time in session"
        unit: "ms"
        tags: ["lap", "summary"]

      - name: total_laps
        type: uint32
        dimensions: 0
        description: "Total laps completed"
        tags: ["lap", "summary"]

      - name: fuel_used
        type: float32
        dimensions: 0
        description: "Total fuel used in session"
        unit: "liters"
        tags: ["fuel", "summary"]

frame:
  description: "Telemetry frame data"
  fields:
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

* Must have at least one value
* All values must have unique names within the enum
* All values must have unique numeric values
* Cannot have fields defined
* All value names and descriptions must be non-empty strings

#### Struct Types

* Must have at least one field
* All fields must have valid types (primitive, enum, or struct)
* All field names and descriptions must be non-empty strings
* All field dimensions must be non-negative

### Session Rules

* Session must have a description
* Session header is required and must have at least one field
* Session footer is optional but must have at least one field if present
* All session fields (header and footer) must follow struct field rules
* Header and footer field names must be unique within their respective sections

### Frame Rules

* Frame must have a description
* Frame must have at least one field
* Frame fields must follow struct field rules
* Frame field names must be unique

### General Rules

* All names and descriptions must be non-empty strings
* All dimensions must be non-negative
* No circular references allowed in custom types
* Frame types must reference valid primitive or custom types
* Version must be "1.0"

## Base Types

The following base types are supported:

* Integers: `int8`, `uint8`, `int16`, `uint16`, `int32`, `uint32`, `int64`, `uint64`
* Floating Point: `float32`, `float64`
* Boolean: `bool`
* Complex: `struct`, `enum`
