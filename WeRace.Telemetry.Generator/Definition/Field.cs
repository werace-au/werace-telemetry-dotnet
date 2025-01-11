namespace WeRace.Telemetry.Generator.Definition;

public sealed class Field : IWithValidation
{
  [RequiredMember] public string Name { get; set; } = "";
  [RequiredMember] public string Type { get; set; } = "";
  [RequiredMember] public int Dimensions { get; set; }
  [RequiredMember] public string Description { get; set; } = "";
  public string Unit { get; set; } = "";
  public string[] Tags { get; set; } = [];

  public void Validate(ValidationContext context)
  {
    if (Name.Trim() is "")
      throw new ValidationException(nameof(Name), "Field name cannot be empty");

    if (Type.Trim() is "")
      throw new ValidationException(nameof(Type), "Field type cannot be empty");

    if (Description.Trim() is "")
      throw new ValidationException(nameof(Description), "Field description cannot be empty");

    if (Dimensions < 0)
      throw new ValidationException(nameof(Dimensions), "Field dimensions cannot be negative");

    // Validate type exists
    try
    {
      TypeValidator.ValidateType(Type, context.Types);
    }
    catch (Exception ex)
    {
      throw new ValidationException(nameof(Type), ex.Message, ex);
    }
  }
}
