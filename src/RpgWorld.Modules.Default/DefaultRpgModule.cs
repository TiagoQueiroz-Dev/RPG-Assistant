using RpgWorld.Domain.Actors.Traits;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Modules.Abstractions;
using RpgWorld.Modules.Abstractions.Definitions;
using RpgWorld.Modules.Default.Actors;
using RpgWorld.Modules.Default.Worlds;

namespace RpgWorld.Modules.Default;

public sealed class DefaultRpgModule : IRpgModule
{
    private readonly DefaultWorldDefinitions _world = new();
    private readonly DefaultActorDefinitions _actors = new();

    public RpgModuleMetadata Metadata { get; } = new(
        "rpgworld.default",
        "RPG World Core",
        new Version(1, 0, 0),
        new Version(1, 0, 0),
        new Version(1, 99, 0));

    public IEnumerable<TerrainDefinition> Terrains => _world.Terrains;
    public IEnumerable<BiomeDefinition> Biomes => _world.Biomes;
    public IEnumerable<ResourceDefinition> Resources => _world.Resources;
    public IEnumerable<TraitDefinition> Traits => _actors.Traits;
    public IEnumerable<CreatureDefinition> Creatures =>
    [
        new("wolf", "Wolf", 35, ["beast", "wildlife"]),
        new("boar", "Boar", 45, ["beast", "wildlife"])
    ];
    public IEnumerable<ItemDefinition> Items =>
    [
        new("travel-ration", "Travel Ration", "consumable", true, ["food"]),
        new("iron-sword", "Iron Sword", "weapon", false, ["melee"])
    ];
    public IEnumerable<RuleDefinition> Rules =>
    [
        new("movement", "Movement", new Dictionary<string, decimal> { ["diagonal-cost"] = 1m }),
        new("perception", "Perception", new Dictionary<string, decimal> { ["base-radius"] = 2m })
    ];
}
