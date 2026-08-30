namespace RpgWorld.Domain.Worlds.Definitions;

public interface IWorldDefinitionCatalog
{
    IReadOnlyCollection<TerrainDefinition> Terrains { get; }

    IReadOnlyCollection<BiomeDefinition> Biomes { get; }

    TerrainDefinition ResolveTerrain(string code);

    BiomeDefinition ResolveBiome(string code);

    bool TryResolveTerrain(string code, out TerrainDefinition? terrain);

    bool TryResolveBiome(string code, out BiomeDefinition? biome);
}
