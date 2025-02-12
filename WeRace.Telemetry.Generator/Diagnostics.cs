using Microsoft.CodeAnalysis;

namespace WeRace.Telemetry.Generator;

internal static class Diagnostics
{
  // Error ranges:
  // TEL001-099: Parse errors
  // TEL100-199: Type generation errors
  // TEL200-299: Reader generation errors
  // TEL300-399: Writer generation errors
  // TEL900-999: Information messages

  public static readonly DiagnosticDescriptor ParseError = new(
      id: "TEL001",
      title: "Telemetry Definition Parse Error",
      messageFormat: "Error parsing telemetry definition: {0}",
      category: "Telemetry",
      DiagnosticSeverity.Error,
      isEnabledByDefault: true);

  public static readonly DiagnosticDescriptor ValidationError = new(
      id: "TEL002",
      title: "Telemetry Definition Validation Error",
      messageFormat: "Telemetry definition validation error: {0}",
      category: "Telemetry",
      DiagnosticSeverity.Error,
      isEnabledByDefault: true);

  public static readonly DiagnosticDescriptor CodeGenerationError = new(
      id: "TEL003",
      title: "Code Generation Error",
      messageFormat: "Error generating code from telemetry definition: {0}",
      category: "Telemetry",
      DiagnosticSeverity.Error,
      isEnabledByDefault: true);

  public static readonly DiagnosticDescriptor TypeGenerationError = new(
      id: "TEL100",
      title: "Type Generation Error",
      messageFormat: "Error generating type {0}: {1}",
      category: "Telemetry",
      DiagnosticSeverity.Error,
      isEnabledByDefault: true);

  public static readonly DiagnosticDescriptor ReaderGenerationError = new(
      id: "TEL200",
      title: "Reader Generation Error",
      messageFormat: "Error generating reader for {0}: {1}",
      category: "Telemetry",
      DiagnosticSeverity.Error,
      isEnabledByDefault: true);

  public static readonly DiagnosticDescriptor WriterGenerationError = new(
      id: "TEL300",
      title: "Writer Generation Error",
      messageFormat: "Error generating writer for {0}: {1}",
      category: "Telemetry",
      DiagnosticSeverity.Error,
      isEnabledByDefault: true);

  public static readonly DiagnosticDescriptor AdditionalFileFound = new(
      id: "TEL900",
      title: "Telemetry Definition File Found",
      messageFormat: "Found telemetry definition file: {0}",
      category: "Telemetry",
      DiagnosticSeverity.Info,
      isEnabledByDefault: true);
}
