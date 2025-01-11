// ValidationContext.cs

namespace WeRace.Telemetry.Generator.Definition;

public sealed class ValidationContext {
  public IReadOnlyDictionary<string, CustomType> Types { get; }

  public ValidationContext(IReadOnlyDictionary<string, CustomType> types) {
    Types = types;
  }
}
