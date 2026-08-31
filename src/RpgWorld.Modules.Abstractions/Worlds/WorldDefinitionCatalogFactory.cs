using RpgWorld.Domain.Worlds.Definitions;

namespace RpgWorld.Modules.Abstractions.Worlds;

public static class WorldDefinitionCatalogFactory
{
    public static WorldDefinitionCatalog Create(IEnumerable<IWorldDefinitionModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        var materializedModules = modules.ToArray();

        return new WorldDefinitionCatalog(
            materializedModules.SelectMany(module => module.Terrains),
            materializedModules.SelectMany(module => module.Biomes),
            materializedModules.SelectMany(module => module.Resources));
    }
}
