using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Modules.Abstractions.Worlds;
using RpgWorld.Modules.Default.Worlds;

namespace RpgWorld.Domain.Tests.Worlds;

public sealed class WorldDefinitionCatalogTests
{
    [Fact]
    public void Registers_and_resolves_all_initial_biomes_with_relevant_properties()
    {
        var catalog = DefaultWorldDefinitions.Catalog;
        var expectedCodes = new[]
        {
            "forest", "desert", "grassland", "mountain", "swamp",
            "snow", "ocean", "river", "volcanic"
        };

        Assert.Equal(expectedCodes, catalog.Biomes.Select(biome => biome.Code));

        var forest = catalog.ResolveBiome("FOREST");
        var terrain = catalog.ResolveTerrain(forest.TerrainCode);

        Assert.True(forest.SupportsClimate(20m, 0.75m));
        Assert.Contains("wildlife", forest.SpawnTags);
        Assert.Contains("wood", forest.ResourceTags);
        Assert.True(terrain.IsTraversable);
        Assert.False(terrain.IsAquatic);
        Assert.True(terrain.MovementCost > 1m);
    }

    [Fact]
    public void Module_can_extend_catalog_without_changing_domain_core()
    {
        var catalog = WorldDefinitionCatalogFactory.Create(
            [new DefaultWorldDefinitions(), new CrystalCavernDefinitions()]);

        var biome = catalog.ResolveBiome("crystal-cavern");

        Assert.Equal("crystal-floor", biome.TerrainCode);
        Assert.Contains("crystals", biome.ResourceTags);
        Assert.Equal("Crystal Floor", catalog.ResolveTerrain(biome.TerrainCode).Name);
    }

    [Fact]
    public void Rejects_biome_that_references_an_unknown_terrain()
    {
        var biome = new BiomeDefinition(
            "void",
            "Void",
            "missing",
            -150m,
            150m,
            0m,
            1m);

        Assert.Throws<ArgumentException>(() =>
            new WorldDefinitionCatalog([], [biome]));
    }

    [Fact]
    public void Tile_resolves_valid_biome_and_rejects_unknown_one()
    {
        var world = World.Create("Aster", 8, 8);
        var position = world.PositionAt(2, 3);

        var tile = world.CreateTile(
            position,
            "forest",
            DefaultWorldDefinitions.Catalog,
            elevation: 10,
            temperatureCelsius: 20m,
            humidity: 0.70m);

        Assert.Equal("forest", tile.BiomeCode);
        Assert.Equal("woodland", tile.TerrainCode);
        Assert.Throws<KeyNotFoundException>(() =>
            world.CreateTile(
                position,
                "unknown",
                DefaultWorldDefinitions.Catalog,
                elevation: 0,
                temperatureCelsius: 20m,
                humidity: 0.50m));
    }

    private sealed class CrystalCavernDefinitions : IWorldDefinitionModule
    {
        public IEnumerable<TerrainDefinition> Terrains =>
        [
            new("crystal-floor", "Crystal Floor", 1.4m, true, false, ["crystals"])
        ];

        public IEnumerable<BiomeDefinition> Biomes =>
        [
            new(
                "crystal-cavern",
                "Crystal Cavern",
                "crystal-floor",
                -10m,
                30m,
                0.20m,
                0.90m,
                resourceTags: ["crystals"],
                spawnTags: ["subterranean"])
        ];
    }
}
