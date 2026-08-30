using RpgWorld.Domain.Worlds.Definitions;

namespace RpgWorld.Modules.Abstractions.Worlds;

public interface IWorldDefinitionModule
{
    IEnumerable<TerrainDefinition> Terrains { get; }

    IEnumerable<BiomeDefinition> Biomes { get; }
}
