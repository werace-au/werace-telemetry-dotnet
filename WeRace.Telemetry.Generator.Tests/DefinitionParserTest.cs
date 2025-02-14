using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace WeRace.Telemetry.Generator.Tests;

public class DefinitionParserTests
{
  private const string EmptyMap = "{}";
  private const string FixturesPath = "Fixtures";

  private readonly DefinitionParser _parser = new();

  private static string LoadFixture(string name)
  {
    var path = Path.Combine(FixturesPath, $"{name}.yaml");
    return File.ReadAllText(path);
  }

  [Fact]
  public void Parse_ValidMinimalDefinition_ReturnsDefinition()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               types: {}
               session:
                 header:
                   fields:
                     - name: type
                       type: uint32
                       dimensions: 0
                       description: "Session type"
               frame:
                 fields:
                   - name: timestamp
                     type: uint64
                     dimensions: 0
                     description: "Timestamp"
                     tags: ["required"]
               """;

    // Act
    var definition = _parser.Parse(yaml);

    // Assert
    definition.Version.Should().Be("1.0");
    definition.Metadata.Title.Should().Be("Test");
    definition.Metadata.Description.Should().Be("Test");
    definition.Types.Should().BeEmpty();

    var field = definition.Frame.Fields[0];
    field.Name.Should().Be("timestamp");
    field.Type.Should().Be("uint64");
    field.Dimensions.Should().Be(0);
    field.Description.Should().Be("Timestamp");
    field.Tags.Should().Equal("required");
  }

  [Fact]
  public void Parse_ComplexTypeDefinition_ReturnsDefinition()
  {
    // Arrange
    var yaml = LoadFixture("valid-complex-type");

    // Act
    var definition = _parser.Parse(yaml);

    // Assert
    definition.Types.Should().ContainKey("vector3");
    var vector3 = definition.Types["vector3"];
    vector3.Fields.Should().HaveCount(3);

    var field = definition.Frame.Fields[0];
    field.Type.Should().Be("vector3");
  }

  [Fact]
  public void Parse_ExampleDefinition_ReturnsDefinition()
  {
    // Arrange
    var yaml = LoadFixture("example");

    // Act
    var definition = _parser.Parse(yaml);

    // Assert
    definition.Types.Should().ContainKey("vector3");
    var vector3 = definition.Types["vector3"];
    vector3.Fields.Should().HaveCount(3);
  }

  [Fact]
  public void Parse_ArrayTypes_ReturnsDefinition()
  {
    // Arrange
    var yaml = LoadFixture("valid-array-types");

    // Act
    var definition = _parser.Parse(yaml);

    // Assert
    definition.Types.Should().ContainKey("sensor_readings");
    var sensorType = definition.Types["sensor_readings"];
    var tempField = sensorType.Fields[0];
    tempField.Dimensions.Should().Be(4);
    tempField.Unit.Should().Be("celsius");

    var frameField = definition.Frame.Fields[0];
    frameField.Dimensions.Should().Be(2);
  }

  [Theory]
  [InlineData("2.0")] // Invalid version
  [InlineData("0.9")] // Invalid version
  public void Parse_UnsupportedVersion_ThrowsException(string version)
  {
    // Arrange
    var yaml = $"""
                version: "{version}"
                metadata:
                  title: "Test"
                  description: "Test"
                types: {EmptyMap}
                session:
                  header:
                    fields:
                      - name: type
                        type: uint32
                        dimensions: 0
                        description: "Session type"
                frame:
                  fields:
                    - name: timestamp
                      type: uint64
                      dimensions: 0
                      description: "Timestamp"
                      tags: ["required"]
                """;

    // Act & Assert
    var action = () => _parser.Parse(yaml);
    action.Should().Throw<ParseException>()
      .WithMessage("*version*");
  }

  [Theory]
  [InlineData("")] // Empty title
  public void Parse_InvalidMetadata_ThrowsException(string? title)
  {
    // Arrange
    var yaml = $"""
                version: "1.0"
                metadata:
                  title: {(title == null ? "~" : $"\"{title}\"")}
                  description: "Test"
                types: {EmptyMap}
                session:
                  header:
                    fields:
                      - name: type
                        type: uint32
                        dimensions: 0
                        description: "Session type"
                frame:
                  fields:
                    - name: timestamp
                      type: uint64
                      dimensions: 0
                      description: "Timestamp"
                      tags: ["required"]
                """;

    // Act & Assert
    var action = () => _parser.Parse(yaml);
    action.Should().Throw<ParseException>();
  }

  [Fact]
  public void Parse_CircularTypeReference_ThrowsException()
  {
    // Arrange
    var yaml = LoadFixture("circular-types");

    // Act & Assert
    var action = () => _parser.Parse(yaml);
    action.Should().Throw<ParseException>()
      .WithMessage("Validation error in Types: Circular reference detected in type_a");
  }

  [Fact]
  public void Parse_InvalidYaml_ThrowsException()
  {
    // Arrange
    var yaml = "invalid: yaml: content: [}";

    // Act & Assert
    var action = () => _parser.Parse(yaml);
    action.Should().Throw<ParseException>();
  }

  [Theory]
  [InlineData("invalid_type")]
  [InlineData("string")]
  [InlineData("datetime")]
  public void Parse_InvalidPrimitiveType_ThrowsException(string type)
  {
    // Arrange
    var yaml = $"""
                version: "1.0"
                metadata:
                  title: "Test"
                  description: "Test"
                types: {EmptyMap}
                session:
                  header:
                    fields:
                      - name: type
                        type: uint32
                        dimensions: 0
                        description: "Session type"
                frame:
                  fields:
                    - name: timestamp
                      type: uint64
                      dimensions: 0
                      description: "Timestamp"
                      tags: ["required"]
                    - name: test
                      type: {type}
                      dimensions: 0
                      description: "Test channel"
                      tags: []
                """;

    // Act & Assert
    var action = () => _parser.Parse(yaml);
    action.Should().Throw<ParseException>()
      .WithMessage("*Invalid type*");
  }

  [Fact]
  public void Parse_ValidEnumDefinition_ReturnsDefinition()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               session:
                 header:
                   fields:
                     - name: type
                       type: uint32
                       dimensions: 0
                       description: "Session type"
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
               frame:
                 fields:
                   - name: log_severity
                     type: log_level
                     dimensions: 0
                     description: "Current log level"
                     tags: []
               """;

    // Act
    var definition = _parser.Parse(yaml);

    // Assert
    definition.Types.Should().ContainKey("log_level");
    var enumType = definition.Types["log_level"];
    enumType.Type.Should().Be("enum");
    enumType.Values.Should().HaveCount(3);

    var debugValue = enumType.Values[0];
    debugValue.Name.Should().Be("Debug");
    debugValue.Value.Should().Be(0);
    debugValue.Description.Should().Be("Debug message");

    var field = definition.Frame.Fields[0];
    field.Type.Should().Be("log_level");
  }

  [Fact]
  public void Parse_EnumWithDuplicateNames_ThrowsException()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               session:
                 header:
                   fields:
                     - name: type
                       type: uint32
                       dimensions: 0
                       description: "Session type"
               types:
                 log_level:
                   type: enum
                   description: "Log levels"
                   values:
                     - name: Debug
                       value: 0
                       description: "Debug message"
                     - name: Debug
                       value: 1
                       description: "Another debug"
               frame:
                 fields:
                   - name: timestamp
                     type: uint64
                     dimensions: 0
                     description: "Timestamp"
                     tags: ["required"]
               """;

    // Act & Assert
    var action = () => _parser.Parse(yaml);
    action.Should().Throw<ParseException>()
      .WithMessage("*Duplicate enum value name*");
  }

  [Fact]
  public void Parse_EnumWithDuplicateValues_ThrowsException()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               session:
                 header:
                   fields:
                     - name: type
                       type: uint32
                       dimensions: 0
                       description: "Session type"
               types:
                 log_level:
                   type: enum
                   description: "Log levels"
                   values:
                     - name: Debug
                       value: 0
                       description: "Debug message"
                     - name: Info
                       value: 0
                       description: "Info message"
               frame:
                 fields:
                   - name: timestamp
                     type: uint64
                     dimensions: 0
                     description: "Timestamp"
                     tags: ["required"]
               """;

    // Act & Assert
    var action = () => _parser.Parse(yaml);
    action.Should().Throw<ParseException>()
      .WithMessage("*Duplicate enum value*");
  }

  [Fact]
  public void Parse_EnumWithNoValues_ThrowsException()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               session:
                 header:
                   fields:
                     - name: type
                       type: uint32
                       dimensions: 0
                       description: "Session type"
               types:
                 log_level:
                   type: enum
                   description: "Log levels"
                   values: []
               frame:
                 fields:
                   - name: timestamp
                     type: uint64
                     dimensions: 0
                     description: "Timestamp"
                     tags: ["required"]
               """;

    // Act & Assert
    var action = () => _parser.Parse(yaml);
    action.Should().Throw<ParseException>()
      .WithMessage("*Enum type must have at least one value*");
  }

  [Fact]
  public void Parse_EnumWithEmptyValueName_ThrowsException()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               session:
                 header:
                   fields:
                     - name: type
                       type: uint32
                       dimensions: 0
                       description: "Session type"
               types:
                 log_level:
                   type: enum
                   description: "Log levels"
                   values:
                     - name: ""
                       value: 0
                       description: "Empty name"
               frame:
                 fields:
                   - name: timestamp
                     type: uint64
                     dimensions: 0
                     description: "Timestamp"
                     tags: ["required"]
               """;

    // Act & Assert
    var action = () => _parser.Parse(yaml);
    action.Should().Throw<ParseException>()
      .WithMessage("*Name cannot be empty*");
  }

  [Fact]
  public void Parse_EnumWithEmptyValueDescription_ThrowsException()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               session:
                 header:
                   fields:
                     - name: type
                       type: uint32
                       dimensions: 0
                       description: "Session type"
               types:
                 log_level:
                   type: enum
                   description: "Log levels"
                   values:
                     - name: Debug
                       value: 0
                       description: ""
               frame:
                 fields:
                   - name: timestamp
                     type: uint64
                     dimensions: 0
                     description: "Timestamp"
                     tags: ["required"]
               """;

    // Act & Assert
    var action = () => _parser.Parse(yaml);
    action.Should().Throw<ParseException>()
      .WithMessage("*Description cannot be empty*");
  }

  [Fact]
  public void Parse_EnumWithFields_ThrowsException()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               session:
                 header:
                   fields:
                     - name: type
                       type: uint32
                       dimensions: 0
                       description: "Session type"
               types:
                 log_level:
                   type: enum
                   description: "Log levels"
                   values:
                     - name: Debug
                       value: 0
                       description: "Debug message"
                   fields:
                     - name: extra
                       type: uint32
                       description: "Extra field"
               frame:
                 fields:
                   - name: timestamp
                     type: uint64
                     dimensions: 0
                     description: "Timestamp"
                     tags: ["required"]
               """;

    // Act & Assert
    var action = () => _parser.Parse(yaml);
    action.Should().Throw<ParseException>()
      .WithMessage("*Enum type cannot have fields*");
  }

  [Fact]
  public void Parse_EnumValueNameWithOnlyWhitespace_ThrowsException()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               session:
                 header:
                   fields:
                     - name: type
                       type: uint32
                       dimensions: 0
                       description: "Session type"
               types:
                 log_level:
                   type: enum
                   description: "Log levels"
                   values:
                     - name: "   "
                       value: 0
                       description: "Invalid name"
               frame:
                 fields:
                   - name: timestamp
                     type: uint64
                     dimensions: 0
                     description: "Timestamp"
                     tags: ["required"]
               """;

    // Act & Assert
    var action = () => _parser.Parse(yaml);
    action.Should().Throw<ParseException>()
      .WithMessage("*Name cannot be empty*");
  }

  [Fact]
  public void Parse_EnumInStructField_ReturnsDefinition()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               session:
                 header:
                   fields:
                     - name: type
                       type: uint32
                       dimensions: 0
                       description: "Session type"
               types:
                 log_level:
                   type: enum
                   description: "Log levels"
                   values:
                     - name: Debug
                       value: 0
                       description: "Debug message"
                     - name: Error
                       value: 1
                       description: "Error message"
                 log_entry:
                   type: struct
                   description: "Log entry"
                   fields:
                     - name: severity
                       type: log_level
                       description: "Log severity"
                     - name: timestamp
                       type: uint64
                       description: "Entry timestamp"
               frame:
                 fields:
                   - name: current_log
                     type: log_entry
                     dimensions: 0
                     description: "Current log entry"
                     tags: []
               """;

    // Act
    var definition = _parser.Parse(yaml);

    // Assert
    definition.Types.Should().ContainKey("log_level");
    definition.Types.Should().ContainKey("log_entry");

    var logEntry = definition.Types["log_entry"];
    logEntry.Fields[0].Type.Should().Be("log_level");
  }

  [Fact]
  public void Parse_ArrayOfEnums_ReturnsDefinition()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               session:
                 header:
                   fields:
                     - name: type
                       type: uint32
                       dimensions: 0
                       description: "Session type"
               types:
                 status_code:
                   type: enum
                   description: "Status codes"
                   values:
                     - name: Ok
                       value: 0
                       description: "OK status"
                     - name: Error
                       value: 1
                       description: "Error status"
               frame:
                 fields:
                   - name: status_history
                     type: status_code
                     dimensions: 10
                     description: "Historical status codes"
                     tags: []
               """;

    // Act
    var definition = _parser.Parse(yaml);

    // Assert
    var field = definition.Frame.Fields[0];
    field.Type.Should().Be("status_code");
    field.Dimensions.Should().Be(10);
  }

  [Fact]
  public void Parse_MultipleEnumsWithSharedValues_ReturnsDefinition()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               session:
                 header:
                   fields:
                     - name: type
                       type: uint32
                       dimensions: 0
                       description: "Session type"
               types:
                 log_level:
                   type: enum
                   description: "Log levels"
                   values:
                     - name: Ok
                       value: 0
                       description: "Ok status"
                 status_code:
                   type: enum
                   description: "Status codes"
                   values:
                     - name: Ok
                       value: 0
                       description: "Ok status"
               frame:
                 fields:
                   - name: log_level
                     type: log_level
                     dimensions: 0
                     description: "Current log level"
                     tags: []
                   - name: status
                     type: status_code
                     dimensions: 0
                     description: "Current status"
                     tags: []
               """;

    // Act
    var definition = _parser.Parse(yaml);

    // Assert
    definition.Types.Should().ContainKey("log_level");
    definition.Types.Should().ContainKey("status_code");
    // Different enums can have the same value names and numeric values
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  [InlineData("\t")]
  public void Parse_EnumValueDescriptionWithWhitespace_ThrowsException(string description)
  {
    // Arrange
    var yaml = $"""
                version: "1.0"
                metadata:
                  title: "Test"
                  description: "Test"
                types:
                  log_level:
                    type: enum
                    description: "Log levels"
                    values:
                      - name: Debug
                        value: 0
                        description: "{description}"
                session:
                  header:
                    fields:
                      - name: type
                        type: uint32
                        dimensions: 0
                        description: "Session type"
                frame:
                  fields:
                    - name: timestamp
                      type: uint64
                      dimensions: 0
                      description: "Timestamp"
                      tags: ["required"]
                """;

    // Act & Assert
    var action = () => _parser.Parse(yaml);
    action.Should().Throw<ParseException>()
      .WithMessage("*Description cannot be empty*");
  }

  [Fact]
  public void Parse_UndefinedEnumTypeInFrame_ThrowsException()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               session:
                 header:
                   fields:
                     - name: type
                       type: uint32
                       dimensions: 0
                       description: "Session type"
               types: {}
               frame:
                 fields:
                   - name: timestamp
                     type: uint64
                     dimensions: 0
                     description: "Timestamp"
                     tags: ["required"]
                   - name: status
                     type: undefined_enum
                     dimensions: 0
                     description: "Status"
                     tags: []
               """;

    // Act & Assert
    var action = () => _parser.Parse(yaml);
    action.Should().Throw<ParseException>()
      .WithMessage("*Invalid type*");
  }

  [Fact]
  public void Parse_ValidEnumWithWhitespaceInNamesAndDescriptions_ReturnsDefinition()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               session:
                 header:
                   fields:
                     - name: type
                       type: uint32
                       dimensions: 0
                       description: "Session type"
               types:
                 log_level:
                   type: enum
                   description: "Log levels"
                   values:
                     - name: "Debug Level"
                       value: 0
                       description: "Debug level message"
                     - name: "Info  Level"
                       value: 1
                       description: "Info  level  message"
               frame:
                 fields:
                   - name: log_level
                     type: log_level
                     dimensions: 0
                     description: "Current log level"
                     tags: ["required"]
               """;

    // Act
    var definition = _parser.Parse(yaml);

    // Assert
    var field = definition.Frame.Fields[0];
    field.Type.Should().Be("log_level");
    field.Name.Should().Be("log_level");
    field.Description.Should().Be("Current log level");
    field.Tags.Should().Equal("required");

    var enumType = definition.Types["log_level"];
    enumType.Values[0].Name.Should().Be("Debug Level");
    enumType.Values[1].Name.Should().Be("Info  Level");
  }

  [Fact]
  public void Parse_AllPrimitiveTypes_ReturnsDefinition()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               session:
                 header:
                   fields:
                     - name: type
                       type: uint32
                       dimensions: 0
                       description: "Session type"
               types:
                 primitives:
                   type: struct
                   description: "All primitive types"
                   fields:
                     - name: int8_val
                       type: int8
                       description: "int8 value"
                     - name: uint8_val
                       type: uint8
                       description: "uint8 value"
                     - name: int16_val
                       type: int16
                       description: "int16 value"
                     - name: uint16_val
                       type: uint16
                       description: "uint16 value"
                     - name: int32_val
                       type: int32
                       description: "int32 value"
                     - name: uint32_val
                       type: uint32
                       description: "uint32 value"
                     - name: int64_val
                       type: int64
                       description: "int64 value"
                     - name: uint64_val
                       type: uint64
                       description: "uint64 value"
                     - name: float32_val
                       type: float32
                       description: "float32 value"
                     - name: float64_val
                       type: float64
                       description: "float64 value"
                     - name: bool_val
                       type: bool
                       description: "bool value"
               frame:
                 fields:
                   - name: test
                     type: primitives
                     dimensions: 0
                     description: "Test channel"
                     tags: []
               """;

    // Act
    var definition = _parser.Parse(yaml);

    // Assert
    var primitives = definition.Types["primitives"];
    primitives.Fields.Should().HaveCount(11); // All primitive types
  }

  [Fact]
  public void Parse_NestedStructs_ReturnsDefinition()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               session:
                 header:
                   fields:
                     - name: type
                       type: uint32
                       dimensions: 0
                       description: "Session type"
               types:
                 vector3:
                   type: struct
                   description: "3D vector"
                   fields:
                     - name: x
                       type: float32
                       description: "X coordinate"
                     - name: y
                       type: float32
                       description: "Y coordinate"
                     - name: z
                       type: float32
                       description: "Z coordinate"
                 transform:
                   type: struct
                   description: "3D transform"
                   fields:
                     - name: position
                       type: vector3
                       description: "Position"
                     - name: rotation
                       type: vector3
                       description: "Rotation"
                     - name: scale
                       type: vector3
                       description: "Scale"
               frame:
                 fields:
                   - name: object_transform
                     type: transform
                     dimensions: 0
                     description: "Object transform"
                     tags: []
               """;

    // Act
    var definition = _parser.Parse(yaml);

    // Assert
    var transform = definition.Types["transform"];
    transform.Fields.Should().HaveCount(3);
    transform.Fields.All(f => f.Type == "vector3").Should().BeTrue();
  }

  [Fact]
  public void Parse_ArraysOfStructs_ReturnsDefinition()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               session:
                 header:
                   fields:
                     - name: type
                       type: uint32
                       dimensions: 0
                       description: "Session type"
               types:
                 point:
                   type: struct
                   description: "2D point"
                   fields:
                     - name: x
                       type: float32
                       description: "X coordinate"
                     - name: y
                       type: float32
                       description: "Y coordinate"
                 polygon:
                   type: struct
                   description: "Polygon"
                   fields:
                     - name: vertices
                       type: point
                       dimensions: 4
                       description: "Vertices"
                     - name: color
                       type: uint32
                       description: "Color"
               frame:
                 fields:
                   - name: polygons
                     type: polygon
                     dimensions: 10
                     description: "Array of polygons"
                     tags: []
               """;

    // Act
    var definition = _parser.Parse(yaml);

    // Assert
    var polygon = definition.Types["polygon"];
    polygon.Fields[0].Dimensions.Should().Be(4);

    var field = definition.Frame.Fields[0];
    field.Dimensions.Should().Be(10);
  }

  [Fact]
  public void Parse_StructFieldWithInvalidType_ThrowsException()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               session:
                 header:
                   fields:
                     - name: type
                       type: uint32
                       dimensions: 0
                       description: "Session type"
               types:
                 invalid_struct:
                   type: struct
                   description: "Invalid struct"
                   fields:
                     - name: field
                       type: nonexistent_type
                       description: "Invalid field"
               frame:
                 fields:
                   - name: timestamp
                     type: uint64
                     dimensions: 0
                     description: "Timestamp"
                     tags: ["required"]
               """;

    // Act & Assert
    var action = () => _parser.Parse(yaml);
    action.Should().Throw<ParseException>()
      .WithMessage("*Invalid type*");
  }

  [Fact]
  public void Parse_StructWithNegativeDimensions_ThrowsException()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               session:
                 header:
                   fields:
                     - name: type
                       type: uint32
                       dimensions: 0
                       description: "Session type"
               types:
                 point:
                   type: struct
                   description: "Point"
                   fields:
                     - name: coords
                       type: float32
                       dimensions: -1
                       description: "Coordinates"
               frame:
                 fields:
                   - name: timestamp
                     type: uint64
                     dimensions: 0
                     description: "Timestamp"
                     tags: ["required"]
               """;

    // Act & Assert
    var action = () => _parser.Parse(yaml);
    action.Should().Throw<ParseException>()
      .WithMessage("*Dimensions cannot be negative*");
  }

  [Fact]
  public void Parse_EmptyStruct_ThrowsException()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               session:
                 header:
                   fields:
                     - name: type
                       type: uint32
                       dimensions: 0
                       description: "Session type"
               types:
                 empty_struct:
                   type: struct
                   description: "Empty struct"
                   fields: []
               frame:
                 fields:
                   - name: timestamp
                     type: uint64
                     dimensions: 0
                     description: "Timestamp"
                     tags: ["required"]
               """;

    // Act & Assert
    var action = () => _parser.Parse(yaml);
    action.Should().Throw<ParseException>()
      .WithMessage("*must have at least one field*");
  }

  [Fact]
  public void Parse_ComplexTypeWithUnits_ReturnsDefinition()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               session:
                 header:
                   fields:
                     - name: type
                       type: uint32
                       dimensions: 0
                       description: "Session type"
               types:
                 sensor_data:
                   type: struct
                   description: "Sensor readings"
                   fields:
                     - name: temperature
                       type: float32
                       description: "Temperature reading"
                       unit: "celsius"
                     - name: pressure
                       type: float32
                       description: "Pressure reading"
                       unit: "pascal"
                     - name: humidity
                       type: float32
                       description: "Humidity reading"
                       unit: "percent"
               frame:
                 fields:
                   - name: sensor_readings
                     type: sensor_data
                     dimensions: 0
                     description: "Sensor data"
                     unit: "combined"
                     tags: ["sensor"]
               """;

    // Act
    var definition = _parser.Parse(yaml);

    // Assert
    var sensorData = definition.Types["sensor_data"];
    sensorData.Fields[0].Unit.Should().Be("celsius");
    sensorData.Fields[1].Unit.Should().Be("pascal");
    sensorData.Fields[2].Unit.Should().Be("percent");

    var field = definition.Frame.Fields[0];
    field.Type.Should().Be("sensor_data");
    field.Unit.Should().Be("combined");
  }

  [Fact]
  public void Parse_ComplexTypeWithEmptyFieldName_ThrowsException()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               session:
                 header:
                   fields:
                     - name: type
                       type: uint32
                       dimensions: 0
                       description: "Session type"
               types:
                 invalid_struct:
                   type: struct
                   description: "Invalid struct"
                   fields:
                     - name: ""
                       type: float32
                       description: "Empty field name"
               frame:
                 fields:
                   - name: timestamp
                     type: uint64
                     dimensions: 0
                     description: "Timestamp"
                     tags: ["required"]
               """;

    // Act & Assert
    var action = () => _parser.Parse(yaml);
    action.Should().Throw<ParseException>()
      .WithMessage("*Name cannot be empty*");
  }

  [Fact]
  public void Parse_ValidSessionWithHeaderAndFooter_ReturnsDefinition()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               session:
                 header:
                   fields:
                     - name: type
                       type: uint32
                       dimensions: 0
                       description: "Session type"
                     - name: driver_id
                       type: uint32
                       dimensions: 0
                       description: "Driver ID"
                 footer:
                   fields:
                     - name: total_laps
                       type: uint32
                       dimensions: 0
                       description: "Total laps completed"
                     - name: best_lap_time
                       type: uint32
                       dimensions: 0
                       description: "Best lap time"
               types: {}
               frame:
                 fields:
                   - name: timestamp
                     type: uint64
                     dimensions: 0
                     description: "Timestamp"
                     tags: ["required"]
               """;

    // Act
    var definition = _parser.Parse(yaml);

    // Assert
    // Verify header
    definition.Session.Header.Fields.Should().HaveCount(2);
    definition.Session.Header.Fields[0].Name.Should().Be("type");
    definition.Session.Header.Fields[1].Name.Should().Be("driver_id");

    // Verify footer
    definition.Session.Footer.Should().NotBeNull();
    definition.Session.Footer?.Fields.Should().HaveCount(2);
    definition.Session.Footer?.Fields[0].Name.Should().Be("total_laps");
    definition.Session.Footer?.Fields[1].Name.Should().Be("best_lap_time");
  }

  [Fact]
  public void Parse_SessionWithoutFooter_ReturnsDefinition()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               session:
                 header:
                   fields:
                     - name: type
                       type: uint32
                       dimensions: 0
                       description: "Session type"
               types: {}
               frame:
                 fields:
                   - name: timestamp
                     type: uint64
                     dimensions: 0
                     description: "Timestamp"
                     tags: ["required"]
               """;

    // Act
    var definition = _parser.Parse(yaml);

    // Assert
    definition.Session.Header.Fields.Should().HaveCount(1);
    definition.Session.Footer.Should().BeNull();
  }

  [Fact]
  public void Parse_SessionWithEmptyHeaderFields_ThrowsException()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               session:
                 header:
                   fields: []
               types: {}
               frame:
                 fields:
                   - name: timestamp
                     type: uint64
                     dimensions: 0
                     description: "Timestamp"
                     tags: ["required"]
               """;

    // Act & Assert
    var action = () => _parser.Parse(yaml);
    action.Should().Throw<ParseException>()
      .WithMessage("*must have at least one field*");
  }

  [Fact]
  public void Parse_SessionWithEmptyFooterFields_ThrowsException()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               session:
                 header:
                   fields:
                     - name: type
                       type: uint32
                       dimensions: 0
                       description: "Session type"
                 footer:
                   fields: []
               types: {}
               frame:
                 fields:
                   - name: timestamp
                     type: uint64
                     dimensions: 0
                     description: "Timestamp"
                     tags: ["required"]
               """;

    // Act & Assert
    var action = () => _parser.Parse(yaml);
    action.Should().Throw<ParseException>()
      .WithMessage("*Session footer must have at least one field*");
  }

  [Fact]
  public void Parse_SessionWithInvalidHeaderFieldType_ThrowsException()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               session:
                 header:
                   fields:
                     - name: type
                       type: invalid_type
                       dimensions: 0
                       description: "Session type"
               types: {}
               frame:
                 fields:
                   - name: timestamp
                     type: uint64
                     dimensions: 0
                     description: "Timestamp"
                     tags: ["required"]
               """;

    // Act & Assert
    var action = () => _parser.Parse(yaml);
    action.Should().Throw<ParseException>()
      .WithMessage("*Invalid type*");
  }

  [Fact]
  public void Parse_SessionWithInvalidFooterFieldType_ThrowsException()
  {
    // Arrange
    var yaml = """
               version: "1.0"
               metadata:
                 title: "Test"
                 description: "Test"
               session:
                 header:
                   fields:
                     - name: type
                       type: uint32
                       dimensions: 0
                       description: "Session type"
                 footer:
                   fields:
                     - name: total_laps
                       type: invalid_type
                       dimensions: 0
                       description: "Total laps"
               types: {}
               frame:
                 fields:
                   - name: timestamp
                     type: uint64
                     dimensions: 0
                     description: "Timestamp"
                     tags: ["required"]
               """;

    // Act & Assert
    var action = () => _parser.Parse(yaml);
    action.Should().Throw<ParseException>()
      .WithMessage("*Invalid type*");
  }
}
