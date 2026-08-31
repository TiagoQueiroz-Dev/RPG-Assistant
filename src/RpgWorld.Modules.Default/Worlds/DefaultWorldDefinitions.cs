using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Modules.Abstractions.Worlds;

namespace RpgWorld.Modules.Default.Worlds;

public sealed class DefaultWorldDefinitions : IWorldDefinitionModule
{
    private static readonly TerrainDefinition[] TerrainDefinitions =
    [
        new("woodland", "Woodland", 1.35m, true, false, ["wood", "herbs"]),
        new("sand", "Sand", 1.50m, true, false, ["minerals"]),
        new("plains", "Plains", 1.00m, true, false, ["food", "fiber"]),
        new("rocky", "Rocky", 2.00m, true, false, ["stone", "ore"]),
        new("wetland", "Wetland", 1.80m, true, false, ["herbs", "peat"]),
        new("snow", "Snow", 1.70m, true, false, ["ice"]),
        new("deep-water", "Deep Water", 2.50m, false, true, ["fish", "salt"]),
        new("freshwater", "Fresh Water", 1.60m, false, true, ["fish", "water"]),
        new("volcanic-rock", "Volcanic Rock", 2.40m, true, false, ["obsidian", "sulfur"])
    ];

    private static readonly BiomeDefinition[] BiomeDefinitions =
    [
        new("forest", "Forest", "woodland", -10m, 35m, 0.45m, 1m, 1.10m, ["wood", "herbs"], ["wildlife", "beasts"]),
        new("desert", "Desert", "sand", 10m, 55m, 0m, 0.25m, 1.15m, ["minerals"], ["reptiles", "scavengers"]),
        new("grassland", "Grassland", "plains", -10m, 40m, 0.20m, 0.70m, 1m, ["food", "fiber"], ["herds", "wildlife"]),
        new("mountain", "Mountain", "rocky", -35m, 25m, 0.05m, 0.90m, 1.35m, ["stone", "ore"], ["climbers", "raptors"]),
        new("swamp", "Swamp", "wetland", 5m, 40m, 0.70m, 1m, 1.30m, ["herbs", "peat"], ["amphibians", "insects"]),
        new("snow", "Snow", "snow", -60m, 5m, 0.05m, 0.90m, 1.25m, ["ice"], ["cold-adapted"]),
        new("ocean", "Ocean", "deep-water", -5m, 40m, 0.50m, 1m, 1m, ["fish", "salt"], ["marine"]),
        new("river", "River", "freshwater", -5m, 40m, 0.40m, 1m, 1m, ["fish", "water"], ["freshwater"]),
        new("volcanic", "Volcanic", "volcanic-rock", 15m, 90m, 0m, 0.50m, 1.50m, ["obsidian", "sulfur"], ["fire-adapted"])
    ];

    private static readonly ResourceDefinition[] ResourceDefinitions =
    [
        new("iron", "Iron", "iron", 120m, habitatTags: ["ore"]),
        new("gold", "Gold", "gold", 60m, habitatTags: ["ore", "minerals"]),
        new("coal", "Coal", "coal", 160m, habitatTags: ["ore", "peat"]),
        new("wood", "Wood", "wood", 100m, regenerationPerWorldHour: 2m, habitatTags: ["wood"]),
        new("stone", "Stone", "stone", 200m, habitatTags: ["stone"]),
        new("food", "Food", "food", 80m, regenerationPerWorldHour: 4m, habitatTags: ["food", "fish"]),
        new("herbs", "Herbs", "herbs", 50m, regenerationPerWorldHour: 1m, habitatTags: ["herbs"])
    ];

    public static WorldDefinitionCatalog Catalog { get; } =
        WorldDefinitionCatalogFactory.Create([new DefaultWorldDefinitions()]);

    public IEnumerable<TerrainDefinition> Terrains => TerrainDefinitions;

    public IEnumerable<BiomeDefinition> Biomes => BiomeDefinitions;

    public IEnumerable<ResourceDefinition> Resources => ResourceDefinitions;
}
