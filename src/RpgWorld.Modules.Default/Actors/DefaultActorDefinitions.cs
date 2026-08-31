using RpgWorld.Domain.Actors.Traits;
using RpgWorld.Modules.Abstractions.Actors;

namespace RpgWorld.Modules.Default.Actors;

public sealed class DefaultActorDefinitions : IActorDefinitionModule
{
    private static readonly TraitDefinition[] TraitDefinitions =
    [
        Trait("brave", "Brave", "Faces danger and is more willing to confront enemies.", ("AttackEnemy", 1.35m), ("Travel", 1.10m)),
        Trait("coward", "Coward", "Avoids confrontation and favors recovering in safety.", ("AttackEnemy", 0.55m), ("Sleep", 1.15m)),
        Trait("greedy", "Greedy", "Places exceptional value on earning and accumulating money.", ("Work", 1.40m)),
        Trait("loyal", "Loyal", "Prioritizes duties and defending allies.", ("Work", 1.10m), ("AttackEnemy", 1.15m)),
        Trait("aggressive", "Aggressive", "Escalates hostile encounters readily.", ("AttackEnemy", 1.50m)),
        Trait("peaceful", "Peaceful", "Strongly disfavors violence when another option exists.", ("AttackEnemy", 0.35m)),
        Trait("curious", "Curious", "Seeks unfamiliar places and new experiences.", ("Travel", 1.45m)),
        Trait("religious", "Religious", "Values disciplined work and restorative routines.", ("Work", 1.10m), ("Sleep", 1.10m)),
        Trait("ambitious", "Ambitious", "Pursues work and opportunities that improve status.", ("Work", 1.30m), ("Travel", 1.15m))
    ];

    public static TraitDefinitionCatalog Catalog { get; } =
        TraitDefinitionCatalogFactory.Create([new DefaultActorDefinitions()]);

    public IEnumerable<TraitDefinition> Traits => TraitDefinitions;

    private static TraitDefinition Trait(
        string code,
        string name,
        string description,
        params (string ActionCode, decimal Multiplier)[] modifiers) =>
        new(
            code,
            name,
            description,
            modifiers.ToDictionary(
                modifier => modifier.ActionCode,
                modifier => modifier.Multiplier,
                StringComparer.OrdinalIgnoreCase));
}
