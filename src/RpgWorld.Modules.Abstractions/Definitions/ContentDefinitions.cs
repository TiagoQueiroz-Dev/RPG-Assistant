namespace RpgWorld.Modules.Abstractions.Definitions;

public sealed record CreatureDefinition
{
    public CreatureDefinition(string code, string name, int maximumHealth, IEnumerable<string>? tags = null)
    {
        Code = ContentDefinitionValidation.Code(code, nameof(code));
        Name = ContentDefinitionValidation.Name(name, nameof(name));
        if (maximumHealth <= 0) throw new ArgumentOutOfRangeException(nameof(maximumHealth));
        MaximumHealth = maximumHealth;
        Tags = ContentDefinitionValidation.Tags(tags);
    }

    public string Code { get; }
    public string Name { get; }
    public int MaximumHealth { get; }
    public IReadOnlySet<string> Tags { get; }
}

public sealed record ItemDefinition
{
    public ItemDefinition(string code, string name, string category, bool stackable, IEnumerable<string>? tags = null)
    {
        Code = ContentDefinitionValidation.Code(code, nameof(code));
        Name = ContentDefinitionValidation.Name(name, nameof(name));
        Category = ContentDefinitionValidation.Code(category, nameof(category));
        Stackable = stackable;
        Tags = ContentDefinitionValidation.Tags(tags);
    }

    public string Code { get; }
    public string Name { get; }
    public string Category { get; }
    public bool Stackable { get; }
    public IReadOnlySet<string> Tags { get; }
}

public sealed record RuleDefinition
{
    public RuleDefinition(string code, string name, IReadOnlyDictionary<string, decimal> parameters)
    {
        Code = ContentDefinitionValidation.Code(code, nameof(code));
        Name = ContentDefinitionValidation.Name(name, nameof(name));
        ArgumentNullException.ThrowIfNull(parameters);
        Parameters = parameters.ToDictionary(
            value => ContentDefinitionValidation.Code(value.Key, nameof(parameters)),
            value => value.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    public string Code { get; }
    public string Name { get; }
    public IReadOnlyDictionary<string, decimal> Parameters { get; }
}

internal static class ContentDefinitionValidation
{
    public static string Code(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Definition code is required.", parameterName);
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > 80 || normalized.Any(character =>
                !char.IsLetterOrDigit(character) && character is not '.' and not '-' and not '_'))
            throw new ArgumentException("Definition code contains unsupported characters.", parameterName);
        return normalized;
    }

    public static string Name(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Definition name is required.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > 200) throw new ArgumentException("Definition name cannot exceed 200 characters.", parameterName);
        return normalized;
    }

    public static IReadOnlySet<string> Tags(IEnumerable<string>? tags) =>
        (tags ?? []).Select(value => Code(value, nameof(tags))).ToHashSet(StringComparer.OrdinalIgnoreCase);
}
