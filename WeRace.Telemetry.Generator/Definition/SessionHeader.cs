using System;

namespace WeRace.Telemetry.Generator.Definition;

public sealed class SessionHeader : IWithValidation
{
  [RequiredMember] public string Description { get; set; } = "";
  [RequiredMember] public Field[] Fields { get; set; } = [];

  public void Validate(ValidationContext context)
  {
    if (Description.Trim() is "")
      throw new ValidationException(nameof(Description), "Header description cannot be empty");

    if (Fields.Length == 0)
      throw new ValidationException(nameof(Fields), "Session header must have at least one field");

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
    }
  }
}
