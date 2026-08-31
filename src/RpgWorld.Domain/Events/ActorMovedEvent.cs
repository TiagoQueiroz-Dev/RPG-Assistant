using RpgWorld.Domain.Worlds;

namespace RpgWorld.Domain.Events;

public sealed record ActorMovedEvent : DomainEvent
{
    public ActorMovedEvent(Guid actorId, Guid worldId, Position origin, Position destination, DateTimeOffset occurredAtUtc)
        : base(occurredAtUtc)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("Actor is required.", nameof(actorId));
        if (worldId == Guid.Empty || origin.WorldId != worldId || destination.WorldId != worldId)
            throw new ArgumentException("Movement positions must belong to the actor's world.", nameof(worldId));
        ActorId = actorId;
        WorldId = worldId;
        Origin = origin;
        Destination = destination;
    }
    public Guid ActorId { get; }
    public Guid WorldId { get; }
    public Position Origin { get; }
    public Position Destination { get; }
}
