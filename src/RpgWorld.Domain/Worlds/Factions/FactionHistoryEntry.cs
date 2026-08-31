namespace RpgWorld.Domain.Worlds.Factions;

public sealed record FactionHistoryEntry(
    Guid Id,
    string EventType,
    string Description,
    Guid? LeaderActorId,
    int MemberCount,
    DateTimeOffset OccurredAtUtc,
    Dictionary<string, string> Metadata);

public static class FactionHistoryEventTypes
{
    public const string Created = "created";
    public const string MemberJoined = "member-joined";
    public const string MemberLeft = "member-left";
    public const string LeaderChanged = "leader-changed";
    public const string CityAssociated = "city-associated";
    public const string CityReleased = "city-released";
    public const string TerritoryClaimed = "territory-claimed";
    public const string TerritoryReleased = "territory-released";
    public const string PowerChanged = "power-changed";
    public const string RelationChanged = "relation-changed";
    public const string DiplomaticStateChanged = "diplomatic-state-changed";
    public const string WarPrevented = "war-prevented";
    public const string WarPreventionLifted = "war-prevention-lifted";
    public const string WarDeclared = "war-declared";
    public const string Dissolved = "dissolved";
}
