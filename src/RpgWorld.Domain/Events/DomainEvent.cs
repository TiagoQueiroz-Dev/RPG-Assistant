namespace RpgWorld.Domain.Events;

public abstract record DomainEvent : IDomainEvent
{
    protected DomainEvent(DateTimeOffset occurredAtUtc)
    {
        EventId = Guid.CreateVersion7();
        var cause = DomainEventCausality.Current;
        CorrelationId = cause?.CorrelationId ?? EventId;
        CausationId = cause?.EventId;
        CausalityDepth = cause is null ? 0 : checked(cause.CausalityDepth + 1);
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
    }

    public Guid EventId { get; }

    public Guid CorrelationId { get; }

    public Guid? CausationId { get; }

    public int CausalityDepth { get; }

    public DateTimeOffset OccurredAtUtc { get; }
}
