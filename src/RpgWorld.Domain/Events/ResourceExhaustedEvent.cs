using RpgWorld.Domain.Worlds.Resources;

namespace RpgWorld.Domain.Events;

public sealed record ResourceExhaustedEvent(
    Guid ResourceId,
    Guid WorldId,
    string ResourceCode,
    ResourceConsumerKind ConsumerKind,
    Guid ConsumerId,
    DateTimeOffset ExhaustedAtUtc)
    : DomainEvent(ExhaustedAtUtc);
