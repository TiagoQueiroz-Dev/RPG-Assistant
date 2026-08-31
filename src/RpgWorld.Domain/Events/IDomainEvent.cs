namespace RpgWorld.Domain.Events;

public interface IDomainEvent
{
    Guid EventId { get; }

    Guid CorrelationId { get; }

    Guid? CausationId { get; }

    int CausalityDepth { get; }

    DateTimeOffset OccurredAtUtc { get; }
}
