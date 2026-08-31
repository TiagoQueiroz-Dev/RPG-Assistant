using RpgWorld.Domain.Worlds.Factions;

namespace RpgWorld.Domain.Events;

public sealed record FactionDiplomaticStateChangedEvent(
    Guid FactionId,
    Guid TargetFactionId,
    Guid WorldId,
    FactionRelationKind PreviousState,
    FactionRelationKind State,
    int Affinity,
    int Tension,
    string Reason,
    Guid? SourceWorldEventId,
    DateTimeOffset OccurredAtUtc)
    : DomainEvent(OccurredAtUtc);
