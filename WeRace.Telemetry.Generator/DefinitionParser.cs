using WeRace.Telemetry.Generator.Definition;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace WeRace.Telemetry.Generator;

public class DefinitionParser
{
  private readonly IDeserializer _deserializer;

  public DefinitionParser()
  {
    _deserializer = new DeserializerBuilder()
      .WithNamingConvention(UnderscoredNamingConvention.Instance)
      .WithEnforceNullability()
      .Build();
  }

  public TelemetryDefinition Parse(string yaml)
  {
    try
    {
      var definition = _deserializer.Deserialize<TelemetryDefinition>(yaml);

      var context = new ValidationContext(definition.Types);
      definition.Validate(context);

      return definition;
    }
    catch (ValidationException ex)
    {
      throw new ParseException($"Validation error in {ex.PropertyPath}: {ex.Message}", ex);
    }
    catch (YamlException ex)
    {
      throw new ParseException($"YAML parsing error at line {ex.Start.Line}: {ex.Message}", ex);
    }
    catch (ArgumentException ex)
    {
      throw new ParseException($"Validation error in {ex.ParamName}: {ex.Message}", ex);
    }
    catch (Exception ex)
    {
      throw new ParseException("Error parsing telemetry definition", ex);
    }
  }
}
