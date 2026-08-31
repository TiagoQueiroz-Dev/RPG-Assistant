using RpgWorld.Domain.Worlds;

namespace RpgWorld.Application.Actors.Movement;

public sealed record ActorMoveResult(
    Guid ActorId,
    Position Origin,
    Position Destination,
    Guid OriginChunkId,
    Guid DestinationChunkId,
    bool CrossedChunkBoundary,
    decimal MovementCost);
