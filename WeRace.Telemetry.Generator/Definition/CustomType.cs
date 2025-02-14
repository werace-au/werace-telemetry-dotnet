namespace WeRace.Telemetry.Generator.Definition;

public sealed class CustomType : IWithValidation
{
    [RequiredMember] public string Type { get; set; } = "";
    [RequiredMember] public string Description { get; set; } = "";
    public Field[] Fields { get; set; } = [];
    public EnumValue[] Values { get; set; } = [];

    public void Validate(ValidationContext context)
    {
        if (Description.Trim() is "")
            throw new ValidationException(nameof(Description), "Description cannot be empty");

        var type = Type.ToLowerInvariant();
        switch (type)
        {
            case "enum":
                ValidateEnum(context);
                break;
            case "flags":
                ValidateFlags(context);
                break;
            case "struct":
                ValidateStruct(context);
                break;
            default:
                throw new ValidationException(nameof(Type), "Type must be struct, enum, or flags");
        }
    }

    private void ValidateEnum(ValidationContext context)
    {
        if (Values.Length == 0)
            throw new ValidationException(nameof(Values), "Enum type must have at least one value");

        if (Fields.Length > 0)
            throw new ValidationException(nameof(Fields), "Enum type cannot have fields");

        var names = new HashSet<string>();
        var values = new HashSet<long>();

        foreach (var value in Values)
        {
            value.Validate(context);

            if (!names.Add(value.Name))
                throw new ValidationException(nameof(Values), $"Duplicate enum value name: {value.Name}");

            if (!values.Add(value.Value))
                throw new ValidationException(nameof(Values), $"Duplicate enum value: {value.Value}");
        }
    }

    private void ValidateFlags(ValidationContext context)
    {
        if (Values.Length == 0)
            throw new ValidationException(nameof(Values), "Flags type must have at least one value");

        if (Fields.Length > 0)
            throw new ValidationException(nameof(Fields), "Flags type cannot have fields");

        var names = new HashSet<string>();
        var values = new HashSet<ulong>();

        foreach (var value in Values)
        {
            value.Validate(context);

            if (!names.Add(value.Name))
                throw new ValidationException(nameof(Values), $"Duplicate flag value name: {value.Name}");

            // Convert to ulong for flag validation
            var flagValue = (ulong)value.Value;

            // Validate flag value is positive and within uint64 range
            if (value.Value < 0)
                throw new ValidationException(nameof(Values), $"Flag value must be non-negative: {value.Name}");

            if (flagValue > ulong.MaxValue)
                throw new ValidationException(nameof(Values), $"Flag value exceeds uint64 range: {value.Name}");

            // For individual flags (not combinations), validate they are powers of 2
            if (IsSingleFlag(flagValue) && !IsPowerOfTwo(flagValue))
                throw new ValidationException(nameof(Values), $"Individual flag value must be a power of 2: {value.Name}");

            if (!values.Add(flagValue))
                throw new ValidationException(nameof(Values), $"Duplicate flag value: {value.Value}");
        }
    }

    private static bool IsSingleFlag(ulong value)
    {
        // If value has only one bit set in binary representation, it's a single flag
        return value != 0 && (value & (value - 1)) == 0;
    }

    private static bool IsPowerOfTwo(ulong value)
    {
        // Check if value is a power of 2
        return value != 0 && (value & (value - 1)) == 0;
    }

    private void ValidateStruct(ValidationContext context)
    {
        if (Values.Length > 0)
            throw new ValidationException(nameof(Values), "Struct type cannot have values");

        if (Fields.Length == 0)
            throw new ValidationException(nameof(Fields), "Struct type must have at least one field");

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

            if (!fieldNames.Add(Fields[i].Name))
                throw new ValidationException(nameof(Fields), $"Duplicate field name: {Fields[i].Name}");
        }
    }
}
