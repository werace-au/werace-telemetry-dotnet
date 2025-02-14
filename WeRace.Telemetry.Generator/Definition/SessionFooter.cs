using System;

namespace WeRace.Telemetry.Generator.Definition;

public sealed class SessionFooter : IWithValidation
{
  [RequiredMember] public Field[] Fields { get; set; } = [];

  public void Validate(ValidationContext context)
  {
    if (Fields.Length == 0)
      throw new ValidationException(nameof(Fields), "Session footer must have at least one field");

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
