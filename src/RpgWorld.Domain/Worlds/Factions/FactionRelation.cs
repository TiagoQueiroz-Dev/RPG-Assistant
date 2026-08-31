namespace RpgWorld.Domain.Worlds.Factions;

public enum FactionRelationKind { Neutral, Allied, Hostile }

public sealed record FactionRelation(
    Guid TargetFactionId,
    FactionRelationKind Kind,
    int Score,
    DateTimeOffset UpdatedAtUtc);
