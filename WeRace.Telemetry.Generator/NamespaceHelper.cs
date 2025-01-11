using Microsoft.CodeAnalysis;

namespace WeRace.Telemetry.Generator;

internal static class NamespaceHelper
{
  public static string GetNamespaceFromFilePath(string filePath, Compilation compilation)
  {
    // Try to get the project's root namespace
    var rootNamespace = TryGetRootNamespace(compilation);
    if (rootNamespace is "") rootNamespace = compilation.AssemblyName ?? "";

    // Default to project name if we can't find anything else
    if (rootNamespace is "") return "Unknown";

    return rootNamespace;
  }

  private static string TryGetRootNamespace(Compilation compilation)
  {
    // Try to find a namespace declaration in the source files
    foreach (var tree in compilation.SyntaxTrees)
    {
      var root = tree.GetRoot();
      var namespaceDecl = root.DescendantNodes()
        .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.NamespaceDeclarationSyntax>()
        .FirstOrDefault();

      if (namespaceDecl != null)
      {
        return namespaceDecl.Name.ToString();
      }
    }

    return string.Empty;
  }
}
