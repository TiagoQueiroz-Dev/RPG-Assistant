namespace RpgWorld.Domain.Worlds.Definitions;

public sealed class ResourceDefinition
{
    public ResourceDefinition(
        string code,
        string name,
        string inventoryItemCode,
        decimal defaultCapacity,
        decimal regenerationPerWorldHour = 0m,
        IEnumerable<string>? habitatTags = null)
    {
        if (defaultCapacity <= 0m)
            throw new ArgumentOutOfRangeException(nameof(defaultCapacity), "Resource capacity must be greater than zero.");
        if (regenerationPerWorldHour < 0m)
            throw new ArgumentOutOfRangeException(nameof(regenerationPerWorldHour));

        Code = DefinitionCode.Normalize(code, nameof(code));
        Name = DefinitionCode.RequiredName(name, nameof(name));
        InventoryItemCode = DefinitionCode.Normalize(inventoryItemCode, nameof(inventoryItemCode));
        DefaultCapacity = defaultCapacity;
        RegenerationPerWorldHour = regenerationPerWorldHour;
        HabitatTags = DefinitionCode.NormalizeTags(habitatTags, nameof(habitatTags));
    }

    public string Code { get; }
    public string Name { get; }
    public string InventoryItemCode { get; }
    public decimal DefaultCapacity { get; }
    public decimal RegenerationPerWorldHour { get; }
    public bool IsRenewable => RegenerationPerWorldHour > 0m;
    public IReadOnlySet<string> HabitatTags { get; }

    public bool Supports(IEnumerable<string> locationResourceTags)
    {
        ArgumentNullException.ThrowIfNull(locationResourceTags);
        return HabitatTags.Count == 0 || locationResourceTags.Any(HabitatTags.Contains);
    }
}
