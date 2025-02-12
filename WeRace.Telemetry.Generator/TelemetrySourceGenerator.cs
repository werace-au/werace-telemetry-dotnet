using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using WeRace.Telemetry.Generator.Definition;

namespace WeRace.Telemetry.Generator;

[Generator]
public class TelemetrySourceGenerator : IIncrementalGenerator
{
  private readonly IGenerator[] _generators;

  public TelemetrySourceGenerator()
  {
    _generators = [
      new TypeGenerator(),
          new ReaderGenerator(),
          new WriterGenerator()
    ];
  }

  public void Initialize(IncrementalGeneratorInitializationContext context)
  {
    // Find all .wrtf.yaml files in AdditionalFiles
    var yamlFiles = context.AdditionalTextsProvider
        .Where(text => text.Path.EndsWith(".wrtf.yaml", StringComparison.OrdinalIgnoreCase) || text.Path.EndsWith(".wrtf.yml", StringComparison.OrdinalIgnoreCase));

    // Report diagnostic for each matching file found
    context.RegisterSourceOutput(yamlFiles.Collect(), (spc, files) =>
    {
      foreach (var file in files)
      {
        var diagnostic = Diagnostic.Create(
                Diagnostics.AdditionalFileFound,
                Location.Create(file.Path, TextSpan.FromBounds(0, 0), new LinePositionSpan()),
                file.Path);
        spc.ReportDiagnostic(diagnostic);
      }
    });

    // Create a provider for the compilation
    var compilationProvider = context.CompilationProvider;

    // Combine the YAML files with the compilation
    IncrementalValueProvider<(Compilation Compilation, (AdditionalText File, TelemetryDefinition? Definition, Exception? Error)[] Definitions)> combined =
        compilationProvider.Combine(yamlFiles.Collect().Select((files, ct) =>
        {
          var results = new List<(AdditionalText, TelemetryDefinition?, Exception?)>();
          var parser = new DefinitionParser();

          foreach (var file in files)
          {
            var yaml = file.GetText(ct)?.ToString();
            if (yaml is null or "")
            {
              results.Add((file, null, new ParseException("File is empty or cannot be read")));
              continue;
            }

            try
            {
              var definition = parser.Parse(yaml);
              results.Add((file, definition, null));
            }
            catch (Exception ex)
            {
              results.Add((file, null, ex));
            }
          }

          return results.ToArray();
        }));

    // Report diagnostics and generate code
    context.RegisterSourceOutput(combined, (spc, data) =>
    {
      var (compilation, defs) = data;
      foreach (var (file, definition, error) in defs)
      {
        if (error != null)
        {
          var diagnostic = Diagnostic.Create(
                  Diagnostics.ParseError,
                  Location.Create(file.Path, TextSpan.FromBounds(0, 0), new LinePositionSpan()),
                  error.Message);

          spc.ReportDiagnostic(diagnostic);
          continue;
        }

        if (definition is null)
        {
          continue;
        }

        try
        {
          GenerateSourceCode(spc, file.Path, definition, compilation);
        }
        catch (Exception ex)
        {
          var diagnostic = Diagnostic.Create(
                  Diagnostics.CodeGenerationError,
                  Location.Create(file.Path, TextSpan.FromBounds(0, 0), new LinePositionSpan()),
                  $"{ex.GetType().Name}: {ex.Message}");

          spc.ReportDiagnostic(diagnostic);
        }
      }
    });
  }

  private void GenerateSourceCode(
      SourceProductionContext context,
      string filePath,
      TelemetryDefinition definition,
      Compilation compilation)
  {
    var fileName = Path.GetFileNameWithoutExtension(filePath).Replace(".wrtf", "");
    var namespaceName = NamespaceHelper.GetNamespaceFromFilePath(filePath, compilation);

    foreach (var generator in _generators)
    {
      try
      {
        generator.Generate(context, namespaceName, fileName, definition, compilation);
      }
      catch (Exception ex)
      {
        context.ReportDiagnostic(Diagnostic.Create(
            Diagnostics.CodeGenerationError,
            Location.None,
            ex.Message));
      }
    }
  }
}
