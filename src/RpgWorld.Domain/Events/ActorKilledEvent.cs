namespace RpgWorld.Domain.Events;

public sealed record ActorKilledEvent : DomainEvent
{
    public ActorKilledEvent(
        Guid actorId,
        Guid? killerId,
        Guid worldId,
        DateTimeOffset occurredAtUtc)
        : base(occurredAtUtc)
    {
        ActorId = Required(actorId, nameof(actorId));
        KillerId = killerId;
        WorldId = Required(worldId, nameof(worldId));
    }

    public Guid ActorId { get; }

    public Guid? KillerId { get; }

    public Guid WorldId { get; }

    private static Guid Required(Guid value, string parameterName) =>
        value == Guid.Empty
            ? throw new ArgumentException("Identifier cannot be empty.", parameterName)
            : value;
}

