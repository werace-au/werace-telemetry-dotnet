using System;
using System.Text;
using Microsoft.CodeAnalysis;
using WeRace.Telemetry.Generator.Definition;

namespace WeRace.Telemetry.Generator;

internal class TypeGenerator : GeneratorBase
{
  private static readonly Dictionary<string, string> TypeMappings = new()
  {
    ["int8"] = "sbyte",
    ["uint8"] = "byte",
    ["int16"] = "short",
    ["uint16"] = "ushort",
    ["int32"] = "int",
    ["uint32"] = "uint",
    ["int64"] = "long",
    ["uint64"] = "ulong",
    ["float32"] = "float",
    ["float64"] = "double",
    ["bool"] = "bool"
  };

  public override void Generate(
      SourceProductionContext context,
      string namespaceName,
      string typeName,
      TelemetryDefinition definition,
      Compilation compilation)
  {
    var source = new StringBuilder();

    // File header
    source.AppendLine("using System;");
    source.AppendLine("using System.Runtime.InteropServices;");
    source.AppendLine();

    source.AppendLine($"namespace {namespaceName};");
    source.AppendLine();

    // Generate custom type declarations
    foreach (var typeEntry in definition.Types)
    {
      try
      {
        GenerateType(source, typeEntry.Key, typeEntry.Value);
      }
      catch (Exception ex)
      {
        context.ReportDiagnostic(Diagnostic.Create(
            Diagnostics.TypeGenerationError,
            Location.None, typeEntry.Key, ex.Message));
        return;
      }
    }

    // Generate session struct
    try
    {
      GenerateSessionStruct(source, typeName, definition.Session);
    }
    catch (Exception ex)
    {
      context.ReportDiagnostic(Diagnostic.Create(
        Diagnostics.TypeGenerationError,
        Location.None,
        [$"{typeName}Session", ex.Message]));
      return;
    }

    // Generate frame struct
    try
    {
      GenerateFrameStruct(source, typeName, definition);
    }
    catch (Exception ex)
    {
      context.ReportDiagnostic(Diagnostic.Create(
          Diagnostics.TypeGenerationError,
          Location.None,
          [typeName, ex.Message]));
      return;
    }

    // Add the source
    AddSource(context, GetHintName(typeName, "Types"), source.ToString());
  }

  private static void GenerateType(StringBuilder source, string typeName, CustomType type)
  {
    switch (type.Type.ToLowerInvariant())
    {
      case "enum":
        GenerateEnum(source, typeName, type);
        break;
      case "struct":
        GenerateStruct(source, typeName, type);
        break;
      default:
        throw new ArgumentException($"Custom types must be struct or enum, got: {type.Type}");
    }
  }

  private static void GenerateEnum(StringBuilder source, string typeName, CustomType type)
  {
    if (type.Description is not "")
    {
      source.AppendLine("/// <summary>");
      source.AppendLine($"/// {type.Description}");
      source.AppendLine("/// </summary>");
    }

    source.AppendLine($"public enum {GetSafeTypeName(typeName)} : uint");
    source.AppendLine("{");

    foreach (var value in type.Values)
    {
      if (value.Description is not "")
      {
        source.AppendLine($"{Indent(1)}/// <summary>");
        source.AppendLine($"{Indent(1)}/// {value.Description}");
        source.AppendLine($"{Indent(1)}/// </summary>");
      }
      source.AppendLine($"{Indent(1)}{value.Name} = {value.Value},");
    }

    source.AppendLine("}");
    source.AppendLine();
  }

  private static void GenerateStruct(StringBuilder source, string typeName, CustomType type)
  {
    if (type.Description is not "")
    {
      source.AppendLine("/// <summary>");
      source.AppendLine($"/// {type.Description}");
      source.AppendLine("/// </summary>");
    }

    source.AppendLine("[StructLayout(LayoutKind.Sequential, Pack = 8)]");
    source.AppendLine($"public struct {GetSafeTypeName(typeName)}");
    source.AppendLine("{");

    foreach (var field in type.Fields)
    {
      GenerateField(source, field);
    }

    source.AppendLine("}");
    source.AppendLine();
  }

  private static void GenerateSessionStruct(StringBuilder source, string typeName, SessionType session)
  {
    if (session.Description is not "")
    {
      source.AppendLine("/// <summary>");
      source.AppendLine($"/// {session.Description}");
      source.AppendLine("/// </summary>");
    }

    // Generate header struct
    source.AppendLine($"public struct {GetSafeTypeName(typeName)}SessionHeader");
    source.AppendLine("{");
    if (session.Header.Description is not "")
    {
      source.AppendLine($"{Indent(1)}/// <summary>");
      source.AppendLine($"{Indent(1)}/// {session.Header.Description}");
      source.AppendLine($"{Indent(1)}/// </summary>");
    }
    foreach (var field in session.Header.Fields)
    {
      GenerateField(source, field);
    }
    source.AppendLine("}");
    source.AppendLine();

    // Generate footer struct if present
    if (session.Footer is not null)
    {
      source.AppendLine($"public struct {GetSafeTypeName(typeName)}SessionFooter");
      source.AppendLine("{");
      if (session.Footer.Description is not "")
      {
        source.AppendLine($"{Indent(1)}/// <summary>");
        source.AppendLine($"{Indent(1)}/// {session.Footer.Description}");
        source.AppendLine($"{Indent(1)}/// </summary>");
      }
      foreach (var field in session.Footer.Fields)
      {
        GenerateField(source, field);
      }
      source.AppendLine("}");
      source.AppendLine();
    }
  }

  private static void GenerateFrameStruct(StringBuilder source, string typeName, TelemetryDefinition definition)
  {
    if (definition.Metadata.Description is not "")
    {
      source.AppendLine("/// <summary>");
      source.AppendLine($"/// {definition.Metadata.Description}");
      source.AppendLine("/// </summary>");
    }

    source.AppendLine("[StructLayout(LayoutKind.Sequential, Pack = 8)]");
    source.AppendLine($"public struct {GetSafeTypeName(typeName)}");
    source.AppendLine("{");

    foreach (var channel in definition.Channels)
    {
      GenerateChannel(source, channel);
    }

    source.AppendLine("}");
    source.AppendLine();
  }

  private static void GenerateField(StringBuilder source, Field field)
  {
    // Documentation
    if (field.Description is not "")
    {
      source.AppendLine($"{Indent(1)}/// <summary>");
      source.AppendLine($"{Indent(1)}/// {field.Description}");
      if (field.Unit is not "")
        source.AppendLine($"{Indent(1)}/// Unit: {field.Unit}");
      source.AppendLine($"{Indent(1)}/// </summary>");
    }

    // Array attribute if needed
    if (field.Dimensions > 0)
    {
      source.AppendLine($"{Indent(1)}[MarshalAs(UnmanagedType.ByValArray, SizeConst = {field.Dimensions})]");
    }

    // Field declaration
    var typeName = TypeMappings.TryGetValue(field.Type.ToLowerInvariant(), out var mappedType)
        ? mappedType
        : GetSafeTypeName(field.Type);

    var fieldType = field.Dimensions > 0 ? $"{typeName}[]" : typeName;
    source.AppendLine($"{Indent(1)}public {fieldType} {GetSafeFieldName(field.Name)};");
  }

  private static void GenerateChannel(StringBuilder source, Channel channel)
  {
    // Documentation
    if (channel.Description is not "")
    {
      source.AppendLine($"{Indent(1)}/// <summary>");
      source.AppendLine($"{Indent(1)}/// {channel.Description}");
      if (channel.Unit is not "")
        source.AppendLine($"{Indent(1)}/// Unit: {channel.Unit}");
      source.AppendLine($"{Indent(1)}/// </summary>");
    }

    if (channel.Tags.Length > 0)
    {
      source.AppendLine($"{Indent(1)}/// <remarks>");
      source.AppendLine($"{Indent(1)}/// Tags: {string.Join(", ", channel.Tags)}");
      source.AppendLine($"{Indent(1)}/// </remarks>");
    }

    // Array attribute if needed
    if (channel.Dimensions > 0)
    {
      source.AppendLine($"{Indent(1)}[MarshalAs(UnmanagedType.ByValArray, SizeConst = {channel.Dimensions})]");
    }

    // Channel field declaration
    var typeName = TypeMappings.TryGetValue(channel.Type.ToLowerInvariant(), out var mappedType)
        ? mappedType
        : GetSafeTypeName(channel.Type);

    var fieldType = channel.Dimensions > 0 ? $"{typeName}[]" : typeName;
    source.AppendLine($"{Indent(1)}public {fieldType} {GetSafeFieldName(channel.Name)};");
  }

  private static string GetSafeTypeName(string name)
  {
    // Split on underscores and other non-alphanumeric characters
    var parts = name.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);

    // Convert each part to PascalCase
    var pascalParts = parts.Select(part =>
        part.Length > 0
            ? char.ToUpperInvariant(part[0]) + part.Substring(1).ToLowerInvariant()
            : "");

    var typeName = string.Concat(pascalParts);

    // Ensure it starts with a letter
    if (typeName.Length > 0 && !char.IsLetter(typeName[0]))
    {
      typeName = "_" + typeName;
    }

    return typeName;
  }

  private static string GetSafeFieldName(string name)
  {
    // Use the same PascalCase conversion for field names
    return GetSafeTypeName(name);
  }
}
