using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Definitions;

namespace RpgWorld.Simulation.Actors;

public interface IActorMovementPolicy
{
    ActorMovementEvaluation Evaluate(
        Actor actor,
        Tile origin,
        Tile destination,
        IWorldDefinitionCatalog definitions);
}
