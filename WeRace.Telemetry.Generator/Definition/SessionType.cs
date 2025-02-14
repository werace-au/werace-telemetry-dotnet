namespace WeRace.Telemetry.Generator.Definition;

public sealed class SessionType : IWithValidation
{
  [RequiredMember] public SessionSection Header { get; set; } = new();
  public SessionSection? Footer { get; set; }

  public void Validate(ValidationContext context)
  {
    try
    {
      Header.ValidateHeader(context);
    }
    catch (ValidationException ex)
    {
      throw new ValidationException("Header", ex.Message, ex);
    }

    if (Footer is not null)
    {
      try
      {
        Footer.ValidateFooter(context);
      }
      catch (ValidationException ex)
      {
        throw new ValidationException("Footer", ex.Message, ex);
      }
    }
  }
}

public sealed class SessionSection : IWithValidation
{
  [RequiredMember] public Field[] Fields { get; set; } = [];

  public void Validate(ValidationContext context)
  {
    ValidateHeader(context);
  }

  public void ValidateHeader(ValidationContext context)
  {
    if (Fields.Length == 0)
      throw new ValidationException(nameof(Fields), "Session header must have at least one field");

    ValidateFields(context);
  }

  public void ValidateFooter(ValidationContext context)
  {
    if (Fields.Length == 0)
      throw new ValidationException(nameof(Fields), "Session footer must have at least one field");

    ValidateFields(context);
  }

  private void ValidateFields(ValidationContext context)
  {
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
