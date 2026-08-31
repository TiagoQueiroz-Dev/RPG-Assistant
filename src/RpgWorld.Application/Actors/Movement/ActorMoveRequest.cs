namespace RpgWorld.Application.Actors.Movement;

public sealed record ActorMoveRequest(Guid ActorId, int DestinationX, int DestinationY);
