using RpgWorld.Domain.Worlds.Definitions;

namespace RpgWorld.Domain.Actors.Traits;

public sealed record TraitDefinition
{
    public TraitDefinition(
        string code,
        string name,
        string description,
        IReadOnlyDictionary<string, decimal> actionScoreMultipliers)
    {
        Code = DefinitionCode.Normalize(code, nameof(code));
        Name = DefinitionCode.RequiredName(name, nameof(name));
        if (Name.Length > 120) throw new ArgumentException("Trait name cannot exceed 120 characters.", nameof(name));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Trait description is required.", nameof(description));
        Description = description.Trim();
        if (Description.Length > 500) throw new ArgumentException("Trait description cannot exceed 500 characters.", nameof(description));
        ArgumentNullException.ThrowIfNull(actionScoreMultipliers);
        if (actionScoreMultipliers.Count == 0)
            throw new ArgumentException("A trait must modify at least one action score.", nameof(actionScoreMultipliers));
        var normalized = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var (actionCode, multiplier) in actionScoreMultipliers)
        {
            if (string.IsNullOrWhiteSpace(actionCode))
                throw new ArgumentException("Modified action code is required.", nameof(actionScoreMultipliers));
            if (multiplier is <= 0m or > 3m)
                throw new ArgumentOutOfRangeException(nameof(actionScoreMultipliers), "Trait multipliers must be greater than zero and at most three.");
            if (!normalized.TryAdd(actionCode.Trim(), multiplier))
                throw new ArgumentException($"Action '{actionCode}' is modified more than once.", nameof(actionScoreMultipliers));
        }
        ActionScoreMultipliers = normalized;
    }

    public string Code { get; }
    public string Name { get; }
    public string Description { get; }
    public IReadOnlyDictionary<string, decimal> ActionScoreMultipliers { get; }

    public bool TryGetMultiplier(string actionCode, out decimal multiplier)
    {
        if (string.IsNullOrWhiteSpace(actionCode)) throw new ArgumentException("Action code is required.", nameof(actionCode));
        return ActionScoreMultipliers.TryGetValue(actionCode.Trim(), out multiplier);
    }
}
