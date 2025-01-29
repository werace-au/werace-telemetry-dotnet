namespace WeRace.Telemetry.Generator.Definition;

public sealed class SessionType : IWithValidation
{
  [RequiredMember] public string Description { get; set; } = "";
  [RequiredMember] public SessionHeader Header { get; set; } = new();
  public SessionFooter? Footer { get; set; }

  public void Validate(ValidationContext context)
  {
    if (Description.Trim() is "")
      throw new ValidationException(nameof(Description), "Description cannot be empty");

    try
    {
      Header.Validate(context);
    }
    catch (ValidationException ex)
    {
      throw new ValidationException("Header", ex.Message, ex);
    }

    if (Footer is not null)
    {
      try
      {
        Footer.Validate(context);
      }
      catch (ValidationException ex)
      {
        throw new ValidationException("Footer", ex.Message, ex);
      }
    }
  }
}
