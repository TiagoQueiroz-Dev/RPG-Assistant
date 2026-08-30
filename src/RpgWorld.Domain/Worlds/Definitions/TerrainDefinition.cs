namespace RpgWorld.Domain.Worlds.Definitions;

public sealed class TerrainDefinition
{
    public TerrainDefinition(
        string code,
        string name,
        decimal movementCost,
        bool isTraversable,
        bool isAquatic,
        IEnumerable<string>? resourceTags = null)
    {
        if (movementCost <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(movementCost),
                "Movement cost must be greater than zero.");
        }

        Code = DefinitionCode.Normalize(code, nameof(code));
        Name = DefinitionCode.RequiredName(name, nameof(name));
        MovementCost = movementCost;
        IsTraversable = isTraversable;
        IsAquatic = isAquatic;
        ResourceTags = DefinitionCode.NormalizeTags(resourceTags, nameof(resourceTags));
    }

    public string Code { get; }

    public string Name { get; }

    public decimal MovementCost { get; }

    public bool IsTraversable { get; }

    public bool IsAquatic { get; }

    public IReadOnlySet<string> ResourceTags { get; }
}
