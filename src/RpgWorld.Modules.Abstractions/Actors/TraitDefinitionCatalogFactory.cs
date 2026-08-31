using RpgWorld.Domain.Actors.Traits;

namespace RpgWorld.Modules.Abstractions.Actors;

public static class TraitDefinitionCatalogFactory
{
    public static TraitDefinitionCatalog Create(IEnumerable<IActorDefinitionModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        return new TraitDefinitionCatalog(modules.SelectMany(module => module.Traits));
    }
}
