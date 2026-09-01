using System.Reflection;
using RpgWorld.Domain.Actors.Traits;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Modules.Abstractions.Definitions;

namespace RpgWorld.Modules.Abstractions;

public sealed class RpgModuleCatalog : IRpgModuleCatalog
{
    public static Version CurrentEngineVersion { get; } = new(1, 0, 0);
    private readonly IReadOnlyDictionary<string, IRpgModule> _modules;

    public RpgModuleCatalog(IEnumerable<IRpgModule> modules, Version? engineVersion = null)
    {
        ArgumentNullException.ThrowIfNull(modules);
        var version = engineVersion ?? CurrentEngineVersion;
        var index = new Dictionary<string, IRpgModule>(StringComparer.OrdinalIgnoreCase);
        foreach (var module in modules)
        {
            ArgumentNullException.ThrowIfNull(module);
            if (!module.Metadata.Supports(version))
                throw new InvalidOperationException(
                    $"Module '{module.Metadata.Id}' is incompatible with engine version {version}.");
            if (!index.TryAdd(module.Metadata.Id, module))
                throw new ArgumentException($"Duplicate RPG module '{module.Metadata.Id}'.", nameof(modules));
        }
        _modules = index;
        AvailableModules = index.Values.Select(value => value.Metadata)
            .OrderBy(value => value.Id, StringComparer.Ordinal).ToArray();
    }

    public IReadOnlyCollection<RpgModuleMetadata> AvailableModules { get; }

    public static RpgModuleCatalog Discover(Version engineVersion, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        var moduleType = typeof(IRpgModule);
        var modules = assemblies.Distinct().SelectMany(assembly => assembly.DefinedTypes)
            .Where(type => !type.IsAbstract && !type.IsInterface && moduleType.IsAssignableFrom(type) &&
                type.GetConstructor(Type.EmptyTypes) is not null)
            .Select(type => (IRpgModule)Activator.CreateInstance(type.AsType())!)
            .ToArray();
        return new RpgModuleCatalog(modules, engineVersion);
    }

    public IRpgContentCatalog Load(IEnumerable<string> moduleIds)
    {
        ArgumentNullException.ThrowIfNull(moduleIds);
        var selected = moduleIds.Select(id =>
        {
            if (string.IsNullOrWhiteSpace(id) || !_modules.TryGetValue(id.Trim(), out var module))
                throw new KeyNotFoundException($"RPG module '{id}' is not registered.");
            return module;
        }).DistinctBy(module => module.Metadata.Id, StringComparer.OrdinalIgnoreCase).ToArray();
        if (selected.Length == 0) throw new ArgumentException("At least one RPG module must be enabled.", nameof(moduleIds));
        return new RpgContentCatalog(selected);
    }

    public IRpgContentCatalog ForCampaign(CampaignModuleSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (selection.CampaignId == Guid.Empty) throw new ArgumentException("Campaign identifier is required.", nameof(selection));
        return Load(selection.EnabledModuleIds);
    }
}

internal sealed class RpgContentCatalog : IRpgContentCatalog
{
    private readonly WorldDefinitionCatalog _world;
    private readonly TraitDefinitionCatalog _traits;
    private readonly IReadOnlyDictionary<string, NpcDefinition> _npcs;
    private readonly IReadOnlyDictionary<string, CreatureDefinition> _creatures;
    private readonly IReadOnlyDictionary<string, ItemDefinition> _items;
    private readonly IReadOnlyDictionary<string, RuleDefinition> _rules;

    public RpgContentCatalog(IReadOnlyCollection<IRpgModule> modules)
    {
        Modules = modules.Select(value => value.Metadata).ToArray();
        _world = new WorldDefinitionCatalog(
            modules.SelectMany(value => value.Terrains),
            modules.SelectMany(value => value.Biomes),
            modules.SelectMany(value => value.Resources));
        _traits = new TraitDefinitionCatalog(modules.SelectMany(value => value.Traits));
        _npcs = Index(modules.SelectMany(value => value.Npcs), value => value.Code, "NPC");
        _creatures = Index(modules.SelectMany(value => value.Creatures), value => value.Code, "creature");
        _items = Index(modules.SelectMany(value => value.Items), value => value.Code, "item");
        _rules = Index(modules.SelectMany(value => value.Rules), value => value.Code, "rule");
        Creatures = _creatures.Values.OrderBy(value => value.Code, StringComparer.Ordinal).ToArray();
        Items = _items.Values.OrderBy(value => value.Code, StringComparer.Ordinal).ToArray();
        Rules = _rules.Values.OrderBy(value => value.Code, StringComparer.Ordinal).ToArray();
        Npcs = _npcs.Values.OrderBy(value => value.Code, StringComparer.Ordinal).ToArray();
    }

    public IReadOnlyCollection<RpgModuleMetadata> Modules { get; }
    public IReadOnlyCollection<TerrainDefinition> Terrains => _world.Terrains;
    public IReadOnlyCollection<BiomeDefinition> Biomes => _world.Biomes;
    public IReadOnlyCollection<ResourceDefinition> Resources => _world.Resources;
    public IReadOnlyCollection<TraitDefinition> Traits => _traits.Traits;
    public IReadOnlyCollection<NpcDefinition> Npcs { get; }
    public IReadOnlyCollection<CreatureDefinition> Creatures { get; }
    public IReadOnlyCollection<ItemDefinition> Items { get; }
    public IReadOnlyCollection<RuleDefinition> Rules { get; }
    public TerrainDefinition ResolveTerrain(string code) => _world.ResolveTerrain(code);
    public BiomeDefinition ResolveBiome(string code) => _world.ResolveBiome(code);
    public ResourceDefinition ResolveResource(string code) => _world.ResolveResource(code);
    public bool TryResolveTerrain(string code, out TerrainDefinition? terrain) => _world.TryResolveTerrain(code, out terrain);
    public bool TryResolveBiome(string code, out BiomeDefinition? biome) => _world.TryResolveBiome(code, out biome);
    public bool TryResolveResource(string code, out ResourceDefinition? resource) => _world.TryResolveResource(code, out resource);
    public TraitDefinition Resolve(string code) => _traits.Resolve(code);
    public bool TryResolve(string code, out TraitDefinition? trait) => _traits.TryResolve(code, out trait);
    public NpcDefinition ResolveNpc(string code) => Resolve(_npcs, code, "NPC");
    public CreatureDefinition ResolveCreature(string code) => Resolve(_creatures, code, "Creature");
    public ItemDefinition ResolveItem(string code) => Resolve(_items, code, "Item");
    public RuleDefinition ResolveRule(string code) => Resolve(_rules, code, "Rule");

    private static IReadOnlyDictionary<string, T> Index<T>(IEnumerable<T> values, Func<T, string> code, string kind)
    {
        var index = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
            if (!index.TryAdd(code(value), value))
                throw new ArgumentException($"Duplicate {kind} definition '{code(value)}'.");
        return index;
    }

    private static T Resolve<T>(IReadOnlyDictionary<string, T> index, string code, string kind)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException($"{kind} code is required.", nameof(code));
        return index.TryGetValue(code.Trim(), out var value)
            ? value
            : throw new KeyNotFoundException($"{kind} definition '{code}' was not found.");
    }
}
