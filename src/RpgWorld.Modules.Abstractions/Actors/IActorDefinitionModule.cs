using RpgWorld.Domain.Actors.Traits;

namespace RpgWorld.Modules.Abstractions.Actors;

public interface IActorDefinitionModule
{
    IEnumerable<TraitDefinition> Traits { get; }
}
