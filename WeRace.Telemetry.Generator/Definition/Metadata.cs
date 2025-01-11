namespace WeRace.Telemetry.Generator.Definition;

public sealed class Metadata : IWithValidation
{
  [RequiredMember] public string Title { get; set; } = "";
  [RequiredMember] public string Description { get; set; } = "";

  public void Validate(ValidationContext context)
  {
    if (Title.Trim() is "")
      throw new ArgumentException("Title cannot be empty", nameof(Title));
    if (Description.Trim() is "")
      throw new ArgumentException("Description cannot be empty", nameof(Description));
  }
}
