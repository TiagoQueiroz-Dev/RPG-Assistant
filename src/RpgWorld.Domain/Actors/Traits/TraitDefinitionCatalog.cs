namespace RpgWorld.Domain.Actors.Traits;

public interface ITraitDefinitionCatalog
{
    IReadOnlyCollection<TraitDefinition> Traits { get; }
    TraitDefinition Resolve(string code);
    bool TryResolve(string code, out TraitDefinition? trait);
}

public sealed class TraitDefinitionCatalog : ITraitDefinitionCatalog
{
    private readonly IReadOnlyDictionary<string, TraitDefinition> _traits;

    public TraitDefinitionCatalog(IEnumerable<TraitDefinition> traits)
    {
        ArgumentNullException.ThrowIfNull(traits);
        var index = new Dictionary<string, TraitDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var trait in traits)
        {
            ArgumentNullException.ThrowIfNull(trait);
            if (!index.TryAdd(trait.Code, trait))
                throw new ArgumentException($"Duplicate trait definition '{trait.Code}'.", nameof(traits));
        }
        _traits = index;
        Traits = index.Values.OrderBy(trait => trait.Code, StringComparer.Ordinal).ToArray();
    }

    public IReadOnlyCollection<TraitDefinition> Traits { get; }

    public TraitDefinition Resolve(string code) =>
        TryResolve(code, out var trait)
            ? trait!
            : throw new KeyNotFoundException($"Trait definition '{code}' was not found.");

    public bool TryResolve(string code, out TraitDefinition? trait)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Trait code is required.", nameof(code));
        return _traits.TryGetValue(code.Trim(), out trait);
    }
}
