using System.Collections.Generic;

namespace WeRace.Telemetry.Generator.Definition;

public class CustomTypeCircularReferenceValidator {
  private readonly Dictionary<string, CustomType> _types;
  private readonly HashSet<string> _visiting;
  private readonly HashSet<string> _visited;

  public CustomTypeCircularReferenceValidator(Dictionary<string, CustomType> types) {
    _types = types;
    _visiting = new HashSet<string>();
    _visited = new HashSet<string>();
  }

  public void ValidateNoCircularReferences() {
    foreach (var typePair in _types) {
      if (!_visited.Contains(typePair.Key)) {
        ValidateType(typePair.Key);
      }
    }
  }

  private void ValidateType(string typeName) {
    if (_visiting.Contains(typeName)) {
      throw new ArgumentException($"Circular reference detected in {typeName}");
    }

    if (_visited.Contains(typeName)) {
      return;
    }

    _visiting.Add(typeName);

    if (_types.TryGetValue(typeName, out var customType)) {
      foreach (var field in customType.Fields) {
        if (_types.ContainsKey(field.Type)) {
          ValidateType(field.Type);
        }
      }
    }

    _visiting.Remove(typeName);
    _visited.Add(typeName);
  }
}
