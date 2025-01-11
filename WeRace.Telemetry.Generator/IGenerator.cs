using Microsoft.CodeAnalysis;
using WeRace.Telemetry.Generator.Definition;

namespace WeRace.Telemetry.Generator;

internal interface IGenerator
{
  /// <summary>
  /// Generate source code for a telemetry definition
  /// </summary>
  void Generate(
    SourceProductionContext context,
    string namespaceName,
    string typeName,
    TelemetryDefinition definition,
    Compilation compilation);
}

/// <summary>
/// Abstract base class for telemetry generators that handles common functionality
/// </summary>
internal abstract class GeneratorBase : IGenerator
{
  protected const string IndentString = "    ";

  public abstract void Generate(
    SourceProductionContext context,
    string namespaceName,
    string typeName,
    TelemetryDefinition definition,
    Compilation compilation);

  protected static string GetTypeName(string baseName, string suffix) =>
    $"{baseName}{suffix}";

  protected static string GetHintName(string typeName, string suffix) =>
    $"{typeName}.{suffix}.g.cs";

  protected static void AddSource(
    SourceProductionContext context,
    string hintName,
    string source)
  {
    context.AddSource(hintName, source);
  }

  protected static string Indent(int level) =>
    string.Concat(Enumerable.Repeat(IndentString, level));
}
