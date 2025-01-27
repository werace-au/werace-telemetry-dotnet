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

          // TODO: Add reader and writer generators
        ];
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register diagnostic logger
        context.RegisterPostInitializationOutput(ctx =>
        {
            ctx.AddSource(
                "GeneratorDebug.g.cs",
                SourceText.From("// Generator initialized", Encoding.UTF8)
            );
        });

        // Find all .wrtf.yaml files in AdditionalFiles
        var yamlFiles = context.AdditionalTextsProvider
            .Where(text => text.Path.EndsWith(".wrtf.yaml", StringComparison.OrdinalIgnoreCase));

        // Add debug output for file discovery
        var fileNames = yamlFiles.Select((file, _) => file.Path);
        context.RegisterSourceOutput(fileNames.Collect(), (spc, files) =>
        {
            var fileList = string.Join("\n", files.Select(f => $"// Found file: {f}"));
            spc.AddSource(
                "FoundFiles.g.cs",
                SourceText.From(fileList, Encoding.UTF8)
            );
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
                        Location.Create(file.Path, TextSpan.FromBounds(0, 0), new()), error.Message);

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

                    // Add debug output for successful generation
                    spc.AddSource(
                        "GenerationSuccess.g.cs",
                        SourceText.From($"// Successfully generated code for {file.Path}", Encoding.UTF8)
                    );
                }
                catch (Exception ex)
                {
                    var diagnostic = Diagnostic.Create(
                        Diagnostics.CodeGenerationError,
                        Location.Create(file.Path, TextSpan.FromBounds(0, 0), new()), $"{ex.GetType().Name}: {ex.Message}");

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
                    Location.None, ex.Message));
            }
        }
    }
}
