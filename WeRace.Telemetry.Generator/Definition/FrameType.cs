namespace WeRace.Telemetry.Generator.Definition;

public sealed class FrameType : IWithValidation
{
  [RequiredMember] public Field[] Fields { get; set; } = [];

  public void Validate(ValidationContext context)
  {
    if (Fields.Length == 0)
      throw new ValidationException(nameof(Fields), "Frame must have at least one field");

    // Validate fields
    var fieldNames = new HashSet<string>();
    for (var i = 0; i < Fields.Length; i++)
    {
      try
      {
        Fields[i].Validate(context);
      }
      catch (ValidationException ex)
      {
        throw new ValidationException($"Fields[{i}]", ex.Message, ex);
      }

      // Check for duplicate field names
      if (!fieldNames.Add(Fields[i].Name))
        throw new ValidationException(nameof(Fields), $"Duplicate field name: {Fields[i].Name}");
    }
  }
}
