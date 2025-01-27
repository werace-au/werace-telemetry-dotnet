using System.Text;
using Microsoft.CodeAnalysis;
using WeRace.Telemetry.Generator.Definition;

namespace WeRace.Telemetry.Generator;

internal class WriterGenerator : GeneratorBase
{
  public override void Generate(
      SourceProductionContext context,
      string namespaceName,
      string typeName,
      TelemetryDefinition definition,
      Compilation compilation)
  {
    var source = new StringBuilder();

    // File header
    source.AppendLine("#nullable enable");
    source.AppendLine();
    source.AppendLine("using System;");
    source.AppendLine("using System.IO;");
    source.AppendLine("using System.Collections.Generic;");
    source.AppendLine();

    source.AppendLine($"namespace {namespaceName};");
    source.AppendLine();

    // Generate specialized writer
    source.AppendLine($"public sealed class {typeName}Writer : IDisposable");
    source.AppendLine("{");

    // Store the underlying writer
    source.AppendLine($"{Indent(1)}private readonly Writer<{typeName}Session, {typeName}> _writer;");
    source.AppendLine();

    // Constructor
    source.AppendLine($"{Indent(1)}public {typeName}Writer(Stream stream, ulong sampleRate, IReadOnlyDictionary<string, string>? metadata = null)");
    source.AppendLine($"{Indent(1)}{{");
    source.AppendLine($"{Indent(2)}_writer = new Writer<{typeName}Session, {typeName}>(stream, sampleRate, metadata);");
    source.AppendLine($"{Indent(1)}}}");
    source.AppendLine();

    // BeginSession method
    source.AppendLine($"{Indent(1)}public void BeginSession({typeName}Session sessionData)");
    source.AppendLine($"{Indent(1)}{{");
    source.AppendLine($"{Indent(2)}_writer.BeginSession(sessionData);");
    source.AppendLine($"{Indent(1)}}}");
    source.AppendLine();

    // WriteFrame method
    source.AppendLine($"{Indent(1)}public void WriteFrame({typeName} frame)");
    source.AppendLine($"{Indent(1)}{{");
    source.AppendLine($"{Indent(2)}_writer.WriteFrame(frame);");
    source.AppendLine($"{Indent(1)}}}");
    source.AppendLine();

    // EndSession method
    source.AppendLine($"{Indent(1)}public void EndSession()");
    source.AppendLine($"{Indent(1)}{{");
    source.AppendLine($"{Indent(2)}_writer.EndSession();");
    source.AppendLine($"{Indent(1)}}}");
    source.AppendLine();

    // Dispose method
    source.AppendLine($"{Indent(1)}public void Dispose()");
    source.AppendLine($"{Indent(1)}{{");
    source.AppendLine($"{Indent(2)}_writer.Dispose();");
    source.AppendLine($"{Indent(1)}}}");

    source.AppendLine("}");

    // Add the source
    AddSource(context, GetHintName(typeName, "Writer"), source.ToString());
  }
}
