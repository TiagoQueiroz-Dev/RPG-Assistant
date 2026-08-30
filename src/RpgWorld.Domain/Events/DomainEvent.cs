namespace RpgWorld.Domain.Events;

public abstract record DomainEvent : IDomainEvent
{
    protected DomainEvent(DateTimeOffset occurredAtUtc)
    {
        EventId = Guid.CreateVersion7();
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid EventId { get; }

    public DateTimeOffset OccurredAtUtc { get; }
}

