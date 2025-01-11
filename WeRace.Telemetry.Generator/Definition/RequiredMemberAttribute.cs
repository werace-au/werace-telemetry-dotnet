namespace WeRace.Telemetry.Generator.Definition;

/// <summary>
/// Polyfill for required members in netstandard2.0
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
internal sealed class RequiredMemberAttribute : Attribute { }
