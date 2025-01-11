namespace WeRace.Telemetry.Generator.Definition;

public sealed class CustomType : IWithValidation
{
    [RequiredMember] public string Type { get; set; } = "";
    [RequiredMember] public string Description { get; set; } = "";
    public Field[] Fields { get; set; } = [];
    public EnumValue[] Values { get; set; } = [];

    public void Validate(ValidationContext context)
    {
        if (Type.Trim() is "")
            throw new ValidationException(nameof(Type), "Type cannot be empty");

        if (Description.Trim() is "")
            throw new ValidationException(nameof(Description), "Description cannot be empty");

        var type = Type.ToLowerInvariant();

        switch (type)
        {
            case "struct":
                ValidateStruct(context);
                break;
            case "enum":
                ValidateEnum(context);
                break;
            default:
                throw new ValidationException(nameof(Type), $"Type must be struct or enum, got: {Type}");
        }
    }

    private void ValidateStruct(ValidationContext context)
    {
        if (Fields.Length == 0)
            throw new ValidationException(nameof(Fields), "Struct type must have at least one field");

        if (Values.Length > 0)
            throw new ValidationException(nameof(Values), "Struct type cannot have enum values");

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

    private void ValidateEnum(ValidationContext context)
    {
        if (Values.Length == 0)
            throw new ValidationException(nameof(Values), "Enum type must have at least one value");

        if (Fields.Length > 0)
            throw new ValidationException(nameof(Fields), "Enum type cannot have fields");

        var usedNames = new HashSet<string>();
        var usedValues = new HashSet<uint>();

        for (var i = 0; i < Values.Length; i++)
        {
            try
            {
                Values[i].Validate(context);

                var normalizedName = Values[i].Name.Trim();
                if (!usedNames.Add(normalizedName))
                    throw new ValidationException(nameof(Values), $"Duplicate enum value name: {normalizedName}");

                if (!usedValues.Add(Values[i].Value))
                    throw new ValidationException(nameof(Values), $"Duplicate enum value: {Values[i].Value}");
            }
            catch (ValidationException ex)
            {
                throw new ValidationException($"Values[{i}]", ex.Message, ex);
            }
        }
    }
}
