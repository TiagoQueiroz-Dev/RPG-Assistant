namespace RpgWorld.Application.Actors.Movement;

public interface IActorMovementService
{
    Task<ActorMoveResult> MoveAsync(
        ActorMoveRequest request,
        CancellationToken cancellationToken = default);
}
