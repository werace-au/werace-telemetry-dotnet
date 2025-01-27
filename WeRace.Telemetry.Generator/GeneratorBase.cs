using Microsoft.CodeAnalysis;
using WeRace.Telemetry.Generator.Definition;

namespace WeRace.Telemetry.Generator;

/// <summary>
/// Abstract base class for telemetry generators that handles common functionality
/// </summary>
internal abstract class GeneratorBase : IGenerator {
  public abstract void Generate(
    SourceProductionContext context,
    string namespaceName,
    string typeName,
    TelemetryDefinition definition,
    Compilation compilation
  );

  protected static string GetTypeName(string baseName, string suffix) =>
    $"{baseName}{suffix}";

  protected static string GetHintName(string typeName, string suffix) =>
    $"{typeName}.{suffix}.g.cs";

  protected static void AddSource(
    SourceProductionContext context,
    string hintName,
    string source
  ) {
    context.AddSource(hintName, source);
  }

  protected static string Indent(int level) => GeneratorHelpers.Indent(level);
}

internal static class GeneratorHelpers {
  private const string IndentString = "    ";

  internal static string Indent(int level) =>
    string.Concat(Enumerable.Repeat(IndentString, level));
}
