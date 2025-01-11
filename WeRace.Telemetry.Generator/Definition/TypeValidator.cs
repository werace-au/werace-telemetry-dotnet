namespace WeRace.Telemetry.Generator.Definition;

public static class TypeValidator
{
  private static readonly HashSet<string> BuiltInTypes = [
    "int8", "uint8", "int16", "uint16", "int32", "uint32",
    "int64", "uint64", "float32", "float64", "bool"
  ];

  public static void ValidateType(string type, IReadOnlyDictionary<string, CustomType> availableTypes)
  {
    var normalizedType = type.ToLowerInvariant();
    if (!BuiltInTypes.Contains(normalizedType) && !availableTypes.ContainsKey(type))
    {
      throw new ValidationException(
        "Type",
        $"Invalid type: {type}. Type must be a built-in type or a defined custom type.");
    }
  }
}
