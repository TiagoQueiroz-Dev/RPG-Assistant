using RpgWorld.Domain.Worlds;

namespace RpgWorld.Domain.Events;

public sealed record ActorCreatedEvent : DomainEvent
{
    public ActorCreatedEvent(Guid actorId, Guid worldId, string actorKind, Position position, DateTimeOffset occurredAtUtc)
        : base(occurredAtUtc)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("Actor is required.", nameof(actorId));
        if (worldId == Guid.Empty || position.WorldId != worldId) throw new ArgumentException("World is invalid.", nameof(worldId));
        ActorId = actorId;
        WorldId = worldId;
        ActorKind = actorKind;
        Position = position;
    }
    public Guid ActorId { get; }
    public Guid WorldId { get; }
    public string ActorKind { get; }
    public Position Position { get; }
}
