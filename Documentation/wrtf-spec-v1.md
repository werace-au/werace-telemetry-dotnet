# We Race Telemetry Format Specification (WRTF)

Version: 1.0

## Table of Contents

1. Overview
2. File Format
3. Header Structure
4. Metadata Dictionary
5. Session Section
6. Frame Section
7. Data Types
8. Alignment Rules
9. Validation Requirements
10. Example Structures

## 1. Overview

e is a binary format designed for storing fixed-rate telemetry data from racing applications. The format supports multiple data channels with varying types including scalars, arrays, and structs, along with rich metadata about the data source and collection environment. The format is optimized for both streaming writes and sequential reads.

## 2. File Format

### Extensions

- `.wrtf` - Fixed rate telemetry data file

### Structure

A WRTF file consists of these main sections:

1. Header (fixed size)
2. Metadata Dictionary
3. One or more Sessions, each containing:
   - Session Header
   - Frame Data
   - Session Footer
4. Document Footer

### Endianness

- All multi-byte values are stored in little-endian format

### Alignment

- All sections and structures are aligned to 8-byte boundaries
- Padding bytes should be set to 0

## 3. Header Structure

| Offset | Size     | Type     | Description                                        |
| ------ | -------- | -------- | -------------------------------------------------- |
| 0      | 8        | char[8]  | Magic number `"WRTF0001"`                          |
| 8      | 8        | uint64   | Version number (1)                                 |
| 16     | 8        | uint64   | Sample rate (Hz)                                   |
| 24     | 8        | uint64   | Start timestamp (microseconds since epoch)         |
| 32     | 4        | uint32   | Number of metadata entries                         |
| 36     | 4        | uint32   | Reserved                                           |
| 40     | Dynamic  | Metadata | Metadata entries in the original dictionary format |
| ...    | Variable | Padding  | Ensures 8-byte alignment                           |

### Metadata Dictionary Structure

The metadata dictionary starts at offset `40`, immediately after the fixed header fields:

1. Number of Metadata Entries: Already stored at offset `32`
2. Metadata Entries:
   - Key length (K) (4 bytes): Length of UTF-8 encoded key
   - Key (K bytes): UTF-8 encoded string
   - Value length (V) (4 bytes): Length of UTF-8 encoded value
   - Value (V bytes): UTF-8 encoded string
   - Padding: Ensures 8-byte alignment

### Example Header

Example header with two metadata entries:

| Offset | Size | Content                | Description                |
| ------ | ---- | ---------------------- | -------------------------- |
| 0      | 8    | `"WRTF0001"`           | Magic number               |
| 8      | 8    | `1`                    | Version number             |
| 16     | 8    | `48000`                | Sample rate (Hz)           |
| 24     | 8    | `1698771650000000`     | Start timestamp            |
| 32     | 4    | `2`                    | Number of metadata entries |
| 36     | 4    | `0`                    | Reserved                   |
| 40     | 4    | `5`                    | Key length ("Track")       |
| 44     | 5    | `"Track"`              | Key                        |
| 49     | 3    | `0`                    | Padding                    |
| 52     | 4    | `20`                   | Value length               |
| 56     | 20   | `"iracing:track/日本"` | Value                      |
| 76     | 4    | `0`                    | Padding                    |
| 80     | 4    | `3`                    | Key length ("Car")         |
| 84     | 3    | `"Car"`                | Key                        |
| 87     | 5    | `0`                    | Padding                    |
| 92     | 4    | `18`                   | Value length               |
| 96     | 18   | `"iracing:car/4321"`   | Value                      |
| 114    | 2    | `0`                    | Padding                    |

## 4. Session Section

### Session Header Format

Each session starts with a fixed header structure:

| Offset | Size | Type          | Description                                          |
| ------ | ---- | ------------- | ---------------------------------------------------- |
| 0      | 8    | char[8]       | Magic number "WRSE0001"                              |
| 8      | [S]  | SessionHeader | Session header data structure (as defined in schema) |

### Session Footer Format

Each session in a complete file must end with a footer structure:

| Offset | Size | Type          | Description                                          |
| ------ | ---- | ------------- | ---------------------------------------------------- |
| 0      | 8    | char[8]       | Magic number "WRSF0001"                              |
| 8      | 8    | uint64        | Number of frames in session                          |
| 16     | 8    | uint64        | Last frame tick                                      |
| 24     | [S]  | SessionFooter | Session footer data structure (as defined in schema) |

### Session Entry Format

| Offset | Size | Type   | Description                              |
| ------ | ---- | ------ | ---------------------------------------- |
| 0      | 8    | uint64 | Session offset from start of file        |
| 8      | 8    | uint64 | Session footer offset from start of file |
| 16     | 8    | uint64 | Number of frames in session              |

## 5. Document Footer

A complete WRTF file must end with a document footer:

| Offset from EOF | Size  | Type      | Description              |
| --------------- | ----- | --------- | ------------------------ |
| -(24 + N24)     | 8     | char[8]   | Start marker "WRDF0001"  |
| -(16 + N24)     | N\*24 | Session[] | Array of session entries |
| -16             | 8     | uint64    | Number of sessions (N)   |
| -8              | 8     | char[8]   | End marker "WRDE0001"    |

### Document Footer Reading

To read the document footer:

1. Find the end marker "WRDE0001" at the end of the file
2. Read the number of sessions (N) from offset -16
3. Read the session entries array
4. Verify the start marker at offset -32-N\*24

### Session Discovery

1. The document footer provides direct access to all sessions
2. Each session has a session footer with frame count and optional data

## 6. Frame Section

### Frame Header Format

Each frame starts with an 8-byte header:

| Offset | Size | Type   | Description      |
| ------ | ---- | ------ | ---------------- |
| 0      | 8    | uint64 | Frame tick count |

The tick count increments by 1 each sample period. Frame ticks must be ordered within a session but are not required to be continuous. Gaps indicate dropped frames.

### Frame Time Calculation

```text
frame_time = session_timestamp + (tick_count * (1_000_000 / sample_rate))
```

### Frame Layout Format

| Section      | Size   | Description                                     |
| ------------ | ------ | ----------------------------------------------- |
| Frame Header | 8      | Frame tick count (uint64)                       |
| Frame Data   | Varies | Sequential frame values according to schema     |
| Padding      | Varies | Zero padding bytes to maintain 8-byte alignment |

The total size of a frame is determined by:

1. Size of the frame header (8 bytes)
2. Sum of all frame data field sizes as defined in the schema
3. Any necessary padding bytes to maintain 8-byte alignment

## 7. Data Types

| Value | Type    | Size (bytes) | Description                                |
| ----- | ------- | ------------ | ------------------------------------------ |
| 0     | int8    | 1            | 8-bit signed integer                       |
| 1     | uint8   | 1            | 8-bit unsigned integer                     |
| 2     | int16   | 2            | 16-bit signed integer                      |
| 3     | uint16  | 2            | 16-bit unsigned integer                    |
| 4     | int32   | 4            | 32-bit signed integer                      |
| 5     | uint32  | 4            | 32-bit unsigned integer                    |
| 6     | int64   | 8            | 64-bit signed integer                      |
| 7     | uint64  | 8            | 64-bit unsigned integer                    |
| 8     | float32 | 4            | 32-bit IEEE 754 floating point             |
| 9     | float64 | 8            | 64-bit IEEE 754 floating point             |
| 10    | bool    | 1            | Boolean value (0 = false, non-zero = true) |
| 11    | struct  | varies       | Composite type containing multiple fields  |
| 12    | enum    | 4            | Enumeration (int32 backing type)           |
| 13    | flags   | 8            | Bit flags (uint64 backing type)            |

## 8. Alignment Rules

1. The header starts at file offset 0
2. The metadata section starts immediately after the header
3. Each metadata entry is padded to 8-byte boundary
4. Session headers and footers are padded to 8-byte boundary
5. Frames are padded to 8-byte boundary
6. All integral types are aligned to their natural boundaries within structs
7. All padding bytes should be set to 0

## 9. Validation Requirements

### Header Validation

1. Magic number must be exactly "WRTF0001"
2. Version number must be 1
3. Base sample rate must be > 0
4. Base timestamp must be > 0
5. Section offsets must be valid
6. Reserved fields must be 0

### Metadata Validation

1. Must contain all required metadata keys
2. Keys must be unique
3. Keys and values must be valid UTF-8
4. Keys must be not be empty strings

### Session Validation

1. Session headers must have valid magic number ("WRSE0001")
2. In complete files:
   - Each session must have a valid session footer with magic number ("WRSF0001")
   - Session footer data must match schema definition
3. Session structures must match schema definition
4. Frame ticks must be ordered within each session

### Document Footer Validation

1. Complete files must have a document footer with:
   - Valid start marker "WRDF0001"
   - Valid end marker "WRDE0001"
2. Number of sessions must match actual number of sessions in file
3. Session offsets must be valid and point to actual session headers
4. Session footer offsets must be valid

## 10. Example Structures

See frame schema documentation for examples of common telemetry structures and field definitions.
