namespace WeRace.Telemetry.Generator.Definition;

public class ValidationException : Exception
{
  public string PropertyPath { get; }

  public ValidationException(string propertyPath, string message)
    : base(message)
  {
    PropertyPath = propertyPath;
  }

  public ValidationException(string propertyPath, string message, Exception innerException)
    : base(message, innerException)
  {
    PropertyPath = propertyPath;
  }
}
