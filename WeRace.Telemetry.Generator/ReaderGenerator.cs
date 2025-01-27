using System.Text;
using Microsoft.CodeAnalysis;
using WeRace.Telemetry.Generator.Definition;

namespace WeRace.Telemetry.Generator;

internal class ReaderGenerator : GeneratorBase
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

        // Generate specialized reader
        source.AppendLine($"public sealed class {typeName}Reader");
        source.AppendLine("{");

        // Store the underlying reader
        source.AppendLine($"{Indent(1)}private readonly Reader<{typeName}Session, {typeName}> _reader;");
        source.AppendLine();

        // Constructor is private - use Open method
        source.AppendLine($"{Indent(1)}private {typeName}Reader(Reader<{typeName}Session, {typeName}> reader)");
        source.AppendLine($"{Indent(1)}{{");
        source.AppendLine($"{Indent(2)}_reader = reader;");
        source.AppendLine($"{Indent(1)}}}");
        source.AppendLine();

        // Properties
        source.AppendLine($"{Indent(1)}public Header Header => _reader.Header;");
        source.AppendLine($"{Indent(1)}public IReadOnlyList<SessionInfo<{typeName}Session>> Sessions => _reader.Sessions;");
        source.AppendLine();

        // Open method
        source.AppendLine($"{Indent(1)}public static {typeName}Reader Open(Stream stream)");
        source.AppendLine($"{Indent(1)}{{");
        source.AppendLine($"{Indent(2)}return new {typeName}Reader(Reader<{typeName}Session, {typeName}>.Open(stream));");
        source.AppendLine($"{Indent(1)}}}");
        source.AppendLine();

        // GetFrames method
        source.AppendLine($"{Indent(1)}public IEnumerable<Frame<{typeName}>> GetFrames(SessionInfo<{typeName}Session> session)");
        source.AppendLine($"{Indent(1)}{{");
        source.AppendLine($"{Indent(2)}return _reader.GetFrames(session);");
        source.AppendLine($"{Indent(1)}}}");

        source.AppendLine("}");

        // Add the source
        AddSource(context, GetHintName(typeName, "Reader"), source.ToString());
    }
}
