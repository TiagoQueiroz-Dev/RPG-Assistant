using RpgWorld.Domain.Actors.Traits;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Modules.Abstractions.Definitions;

namespace RpgWorld.Modules.Abstractions;

public sealed class RpgContentOverlayCatalog : IRpgContentCatalog
{
    private readonly WorldDefinitionCatalog _world;
    private readonly ITraitDefinitionCatalog _traits;
    private readonly IReadOnlyDictionary<string, CreatureDefinition> _creatures;
    private readonly IReadOnlyDictionary<string, ItemDefinition> _items;
    private readonly IReadOnlyDictionary<string, RuleDefinition> _rules;

    public RpgContentOverlayCatalog(
        IRpgContentCatalog moduleContent,
        IEnumerable<CreatureDefinition>? creatures = null,
        IEnumerable<ItemDefinition>? items = null,
        IEnumerable<BiomeDefinition>? biomes = null,
        IEnumerable<RuleDefinition>? rules = null)
    {
        ArgumentNullException.ThrowIfNull(moduleContent);
        Modules = moduleContent.Modules;
        _traits = moduleContent;
        _creatures = Overlay(moduleContent.Creatures, creatures, value => value.Code);
        _items = Overlay(moduleContent.Items, items, value => value.Code);
        _rules = Overlay(moduleContent.Rules, rules, value => value.Code);
        var mergedBiomes = Overlay(moduleContent.Biomes, biomes, value => value.Code).Values;
        _world = new WorldDefinitionCatalog(moduleContent.Terrains, mergedBiomes, moduleContent.Resources);
        Creatures = Values(_creatures);
        Items = Values(_items);
        Rules = Values(_rules);
    }

    public IReadOnlyCollection<RpgModuleMetadata> Modules { get; }
    public IReadOnlyCollection<TerrainDefinition> Terrains => _world.Terrains;
    public IReadOnlyCollection<BiomeDefinition> Biomes => _world.Biomes;
    public IReadOnlyCollection<ResourceDefinition> Resources => _world.Resources;
    public IReadOnlyCollection<TraitDefinition> Traits => _traits.Traits;
    public IReadOnlyCollection<CreatureDefinition> Creatures { get; }
    public IReadOnlyCollection<ItemDefinition> Items { get; }
    public IReadOnlyCollection<RuleDefinition> Rules { get; }
    public TerrainDefinition ResolveTerrain(string code) => _world.ResolveTerrain(code);
    public BiomeDefinition ResolveBiome(string code) => _world.ResolveBiome(code);
    public ResourceDefinition ResolveResource(string code) => _world.ResolveResource(code);
    public bool TryResolveTerrain(string code, out TerrainDefinition? value) => _world.TryResolveTerrain(code, out value);
    public bool TryResolveBiome(string code, out BiomeDefinition? value) => _world.TryResolveBiome(code, out value);
    public bool TryResolveResource(string code, out ResourceDefinition? value) => _world.TryResolveResource(code, out value);
    public TraitDefinition Resolve(string code) => _traits.Resolve(code);
    public bool TryResolve(string code, out TraitDefinition? value) => _traits.TryResolve(code, out value);
    public CreatureDefinition ResolveCreature(string code) => Resolve(_creatures, code, "Creature");
    public ItemDefinition ResolveItem(string code) => Resolve(_items, code, "Item");
    public RuleDefinition ResolveRule(string code) => Resolve(_rules, code, "Rule");

    private static IReadOnlyDictionary<string, T> Overlay<T>(
        IEnumerable<T> defaults,
        IEnumerable<T>? overrides,
        Func<T, string> code)
    {
        var values = defaults.ToDictionary(code, StringComparer.OrdinalIgnoreCase);
        foreach (var value in overrides ?? []) values[code(value)] = value;
        return values;
    }

    private static IReadOnlyCollection<T> Values<T>(IReadOnlyDictionary<string, T> values) =>
        values.OrderBy(value => value.Key, StringComparer.Ordinal).Select(value => value.Value).ToArray();

    private static T Resolve<T>(IReadOnlyDictionary<string, T> values, string code, string kind)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException($"{kind} code is required.", nameof(code));
        return values.TryGetValue(code.Trim(), out var value)
            ? value
            : throw new KeyNotFoundException($"{kind} definition '{code}' was not found.");
    }
}
