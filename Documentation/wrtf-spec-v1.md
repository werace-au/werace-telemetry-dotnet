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

WRTF is a binary format designed for storing fixed-rate telemetry data from racing applications. The format supports
multiple data channels with varying types including scalars, arrays, and structs, along with rich metadata about the
data source and collection environment. The format is optimized for both streaming writes and sequential reads.

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
- Session Footer (if complete)

### Endianness

- All multi-byte values are stored in little-endian format

### Alignment

- All sections and structures are aligned to 8-byte boundaries
- Padding bytes should be set to 0

## 3. Header Structure

| Offset | Size                      | Type     | Description                                                         |
|--------|---------------------------|----------|---------------------------------------------------------------------|
| 0      | 8                         | char[8]  | Magic number `"WRTF0001"`                                           |
| 8      | 8                         | uint64   | Version number (1)                                                  |
| 16     | 8                         | uint64   | Sample rate (Hz)                                                    |
| 24     | 8                         | uint64   | Start timestamp (microseconds since epoch)                          |
| 32     | 4                         | uint32   | Number of metadata entries                                          |
| 36     | 4                         | uint32   | Reserved                                                            |
| 40     | Dynamic                   | Metadata | Metadata entries in the original dictionary format (detailed below) |
| ...    | Variable (8-byte aligned) | Padding  | Ensures the total header aligns to an 8-byte boundary.              |

### Embedded Metadata Dictionary Structure

The metadata dictionary follows its original format and directly starts at offset `40`, immediately after the fixed
header fields:

1. **Number of Metadata Entries**: Already stored at offset `32`.
2. Metadata Entries:

- **Key length (K)** (4 bytes): Length (in bytes) of the UTF-8 encoded key.
- **Key** (K bytes): UTF-8 encoded string for the key.
- **Value length (V)** (4 bytes): Length (in bytes) of the UTF-8 encoded value.
- **Value** (V bytes): UTF-8 encoded string for the value.
- **Padding**: Ensures each entry is aligned to an 8-byte boundary.

This layout allows the metadata section to grow dynamically within the header to accommodate varying amounts of
metadata, as needed.

### Example (Expanded Header)

Let’s assume the updated header, including metadata, contains 2 metadata entries with the following keys and values:

- `Track = iracing:track/日本`
  _(Here, "日本" means "Japan" in Japanese, represented in UTF-8 by three bytes per character.)_
- `Car = iracing:car/4321`

### Example: Header with UTF-8 Extended String

| Offset  | Size   | Content                                                |
|---------|--------|--------------------------------------------------------|
| 0       | 8      | `"WRTF0001"` (Magic number)                            |
| 8       | 8      | `1` (Version number)                                   |
| 16      | 8      | `48000` (Sample rate in Hz)                            |
| 24      | 8      | `1698771650000000` (Start timestamp)                   |
| 32      | 4      | `2` (Number of metadata entries)                       |
| 36      | 4      | `0` (Reserved)                                         |
| **40**  | **4**  | `5` (Key length for "Track")                           |
| **44**  | **5**  | `"Track"` (UTF-8 encoded key)                          |
| **49**  | **3**  | **Padding** (to align to 8 bytes)                      |
| **52**  | **4**  | `20` (Value length for "iracing:track/日本")             |
| **56**  | **20** | `"iracing:track/日本"` (UTF-8 value, where 日本 = 6 bytes) |
| **76**  | **4**  | **Padding** (to align to 8 bytes)                      |
| **80**  | **4**  | `3` (Key length for "Car")                             |
| **84**  | **3**  | `"Car"` (UTF-8 encoded key)                            |
| **87**  | **5**  | **Padding** (to align to 8 bytes)                      |
| **92**  | **4**  | `18` (Value length for "iracing:car/4321")             |
| **96**  | **18** | `"iracing:car/4321"` (UTF-8 value)                     |
| **114** | **2**  | **Padding** (to align to 8 bytes)                      |

### Explanation of the Layout

1. **UTF-8 Value for "Track"**:

- The value is `"iracing:track/日本"`:
  - `"日本"` consists of **6 bytes** in UTF-8 (3 bytes for each character).
  - Total value length: `"iracing:track/"` (14 bytes) + `"日本"` (6 bytes) = **20 bytes**.

- Proper padding (4 bytes) is added after this value to align it to the next 8-byte boundary.

1. **Metadata Header Fields**:

- Each metadata entry has its **key**, key length, **value**, and value length, followed by padding to maintain 8-byte
  alignment.

1. **Second Entry ("Car")**:

- `Car = iracing:car/4321` is handled as before:
  - Key length: `3`.
  - Key: `"Car"`.
  - Value length: `18`.
  - Value: `"iracing:car/4321"`.

1. **Alignment**:

- Padding ensures each entry, and the entire header, is aligned to an 8-byte boundary.

## 5. Session Section

Sessions organize telemetry data into logical groups representing different periods of recording (e.g., practice,
qualifying, race segments, or driver changes).

### Session Headers

Each session starts with a fixed header structure:

```
Offset  Size    Type        Description
0       8       char[8]     Magic number "WRSE0001"
8       [S]     Session     Session data structure (as defined in schema)
[Pad to 8-byte boundary]
```

### Session Footer

Each completed session ends with a fixed footer structure:

```
Offset  Size    Type        Description
0       8       char[8]     Magic number "WRSF0001"
8       8       uint64      Number of frames in session
16      8       uint64      Last frame tick
[Pad to 8-byte boundary]
```

The footer is written when the session is complete. For files that are still being written or were not properly closed,
the footer may be missing.

### Session Discovery

Sessions can be discovered by:

1. Forward scan - following session headers until EOF
2. Reverse scan - starting from EOF and looking for footers (completed files only)

## 6. Frame Section

Frames follow immediately after each session header and continue until either:

- A session footer is encountered
- Another session header is encountered (identified by magic number)
- End of file is reached

### Frame Header

Each frame starts with an 8-byte header:

```
Offset  Size    Type        Description
0       8       uint64      Frame tick count (increments by 1 each sample period)
```

The tick count represents the number of sample periods since recording started. Frame ticks must be ordered (increasing)
within a session but are not required to be continuous. Gaps in tick values indicate skipped or dropped frames.

The actual time of a frame can be calculated as:

```
frame_time = session_timestamp + (tick_count * (1_000_000 / sample_rate))
```

### Frame Layout

```
[Frame Header][Channel 1 Value][Channel 2 Value]...[Channel N Value][Padding to 8 bytes]
```

Frame Size:
The size of each frame is fixed and can be determined by:

1. 8 bytes for frame header
2. Sum of all channel sizes (based on their types and dimensions)
3. Padding to maintain 8-byte alignment

## 7. Data Types

```
Value   Type        Size (bytes)  Description
0       int8        1            8-bit signed integer
1       uint8       1            8-bit unsigned integer
2       int16       2            16-bit signed integer
3       uint16      2            16-bit unsigned integer
4       int32       4            32-bit signed integer
5       uint32      4            32-bit unsigned integer
6       int64       8            64-bit signed integer
7       uint64      8            64-bit unsigned integer
8       float32     4            32-bit IEEE 754 floating point
9       float64     8            64-bit IEEE 754 floating point
10      bool        1            Boolean value (0 = false, non-zero = true)
11      struct      varies       Composite type containing multiple fields
12      enum        4            Enumeration (uint32 backing type)
```

## 8. Alignment Rules

1. The header starts at file offset 0
2. The metadata section starts immediately after the header
3. Each metadata entry is padded to 8-byte boundary
4. Each session header, frame, and footer is padded to 8-byte boundary
5. All integral types are aligned to their natural boundaries within structs
6. All padding bytes should be set to 0

## 9. Validation Requirements

A valid WRTF file must meet these requirements:

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
4. "created_at" must be valid ISO 8601

### Session Validation

1. Session headers must have valid magic number ("WRSE0001")
2. Session footers (if present) must have valid magic number ("WRSF0001")
3. Session structures must match schema definition
4. Frame ticks must be ordered within each session

## 10. Example Structures

See channel schema documentation for examples of common telemetry structures and channel definitions.
