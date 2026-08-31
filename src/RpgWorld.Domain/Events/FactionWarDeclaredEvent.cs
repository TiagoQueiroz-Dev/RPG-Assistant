using RpgWorld.Domain.Worlds.Factions;

namespace RpgWorld.Domain.Events;

public sealed record FactionWarDeclaredEvent(
    Guid FactionId,
    Guid TargetFactionId,
    Guid WorldId,
    FactionWarScore WarScore,
    bool ForcedByGameMaster,
    string Reason,
    DateTimeOffset OccurredAtUtc)
    : DomainEvent(OccurredAtUtc);
