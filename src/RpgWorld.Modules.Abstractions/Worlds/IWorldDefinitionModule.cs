using RpgWorld.Domain.Worlds.Definitions;

namespace RpgWorld.Modules.Abstractions.Worlds;

public interface IWorldDefinitionModule
{
    IEnumerable<TerrainDefinition> Terrains => [];

    IEnumerable<BiomeDefinition> Biomes => [];

    IEnumerable<ResourceDefinition> Resources => [];
}
