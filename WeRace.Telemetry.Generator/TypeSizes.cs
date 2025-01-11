using System.Collections.Immutable;

namespace WeRace.Telemetry.Generator;

internal static class TypeSizes
{
  public static readonly ImmutableDictionary<string, int> BaseTypeSizes =
    new Dictionary<string, int>
    {
      ["int8"] = 1,
      ["uint8"] = 1,
      ["int16"] = 2,
      ["uint16"] = 2,
      ["int32"] = 4,
      ["uint32"] = 4,
      ["int64"] = 8,
      ["uint64"] = 8,
      ["float32"] = 4,
      ["float64"] = 8,
      ["bool"] = 1
    }.ToImmutableDictionary();
}
