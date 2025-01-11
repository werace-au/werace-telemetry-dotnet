namespace WeRace.Telemetry.Generator.Definition;

public sealed class EnumValue : IWithValidation {
  [RequiredMember] public string Name { get; set; } = "";

  [RequiredMember] public uint Value { get; set; }

  [RequiredMember] public string Description { get; set; } = "";

  public void Validate(ValidationContext context) {
    if (Name.Trim().Length == 0)
      throw new ArgumentException("Name cannot be empty", nameof(Name));
    if (Description.Trim().Length == 0)
      throw new ArgumentException("Description cannot be empty", nameof(Description));
  }
}
