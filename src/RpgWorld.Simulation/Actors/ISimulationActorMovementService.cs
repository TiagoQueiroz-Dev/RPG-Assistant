using RpgWorld.Application.Actors.Movement;

namespace RpgWorld.Simulation.Actors;

// Engine callers already hold the world command gate and supply simulation time.
public interface ISimulationActorMovementService
{
    Task<ActorMoveResult> MoveDuringTickAsync(ActorMoveRequest request, Guid worldId, DateTimeOffset instant,
        CancellationToken cancellationToken = default);
}
