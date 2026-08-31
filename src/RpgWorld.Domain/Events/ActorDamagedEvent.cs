namespace RpgWorld.Domain.Events;

public sealed record ActorDamagedEvent : DomainEvent
{
    public ActorDamagedEvent(
        Guid actorId,
        Guid? sourceActorId,
        Guid worldId,
        int damage,
        int remainingHealth,
        DateTimeOffset occurredAtUtc) : base(occurredAtUtc)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("Actor is required.", nameof(actorId));
        if (sourceActorId == Guid.Empty) throw new ArgumentException("Source actor cannot be empty.", nameof(sourceActorId));
        if (worldId == Guid.Empty) throw new ArgumentException("World is required.", nameof(worldId));
        if (damage <= 0) throw new ArgumentOutOfRangeException(nameof(damage));
        if (remainingHealth < 0) throw new ArgumentOutOfRangeException(nameof(remainingHealth));
        ActorId = actorId;
        SourceActorId = sourceActorId;
        WorldId = worldId;
        Damage = damage;
        RemainingHealth = remainingHealth;
    }
    public Guid ActorId { get; }
    public Guid? SourceActorId { get; }
    public Guid WorldId { get; }
    public int Damage { get; }
    public int RemainingHealth { get; }
}
