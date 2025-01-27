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
