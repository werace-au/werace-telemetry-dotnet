namespace WeRace.Telemetry.Generator.Definition;

public sealed class TelemetryDefinition : IWithValidation
{
  [RequiredMember] public string Version { get; set; } = "1.0";
  [RequiredMember] public Metadata Metadata { get; set; } = new();
  [RequiredMember] public Dictionary<string, CustomType> Types { get; set; } = [];
  [RequiredMember] public SessionType Session { get; set; } = new();
  [RequiredMember] public FrameType Frame { get; set; } = new();

  public void Validate(ValidationContext context)
  {
    if (Version.Trim() != "1.0")
      throw new ValidationException("Version", "Expected version 1.0");

    try
    {
      Metadata.Validate(context);
    }
    catch (ValidationException ex)
    {
      throw new ValidationException("Metadata", ex.Message, ex);
    }

    try
    {
      Session.Validate(context);
    }
    catch (ValidationException ex)
    {
      throw new ValidationException("Session", ex.Message, ex);
    }

    // Validate individual types and their names
    foreach (var entry in Types)
    {
      if (entry.Key.Trim() is "")
        throw new ValidationException("Types", "Type name cannot be empty");

      try
      {
        entry.Value.Validate(context);
      }
      catch (ValidationException ex)
      {
        throw new ValidationException($"Types.{entry.Key}", ex.Message, ex);
      }
    }

    // Validate no circular references in custom types
    try
    {
      var circularReferenceValidator = new CustomTypeCircularReferenceValidator(Types);
      circularReferenceValidator.ValidateNoCircularReferences();
    }
    catch (Exception ex)
    {
      throw new ValidationException("Types", ex.Message, ex);
    }

    // Validate frame
    try
    {
      Frame.Validate(context);
    }
    catch (ValidationException ex)
    {
      throw new ValidationException("Frame", ex.Message, ex);
    }
  }
}
