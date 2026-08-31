using RpgWorld.Domain.Worlds.Factions;

namespace RpgWorld.Application.Worlds.Factions;

public sealed record FactionTerritoryPosition(int X, int Y);

public sealed record CreateFactionRequest(
    Guid WorldId,
    string Name,
    FactionType Type,
    Guid LeaderActorId,
    decimal InitialWealth,
    decimal InitialMilitaryPower,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyCollection<FactionTerritoryPosition>? Territory = null);

public sealed record FactionRelationView(
    Guid TargetFactionId,
    string State,
    int Affinity,
    int Tension,
    bool IsVassal,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<FactionRelationChangeView> History);

public sealed record FactionRelationChangeView(
    Guid ChangeId,
    string Source,
    string Reason,
    int AffinityDelta,
    int TensionDelta,
    int PreviousAffinity,
    int Affinity,
    int PreviousTension,
    int Tension,
    string PreviousState,
    string State,
    Guid? SourceEventId,
    DateTimeOffset OccurredAtUtc);

public sealed record FactionMasterView(
    Guid FactionId,
    Guid WorldId,
    string Name,
    string Type,
    string Status,
    Guid? LeaderActorId,
    IReadOnlyList<Guid> MemberActorIds,
    IReadOnlyList<Guid> ControlledCityIds,
    IReadOnlyList<FactionTerritoryPosition> Territory,
    decimal Wealth,
    decimal MilitaryPower,
    IReadOnlyList<FactionRelationView> Relations,
    IReadOnlyList<FactionHistoryEntry> History,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DissolvedAtUtc);

public interface IFactionService
{
    Task<FactionMasterView> CreateAsync(CreateFactionRequest request, CancellationToken cancellationToken = default);
    Task<FactionMasterView?> GetAsync(Guid factionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FactionMasterView>> ListByWorldAsync(Guid worldId, CancellationToken cancellationToken = default);
    Task<FactionMasterView> AddMemberAsync(Guid factionId, Guid actorId, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken = default);
    Task<FactionMasterView> RemoveMemberAsync(Guid factionId, Guid actorId, string reason, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken = default);
    Task<FactionMasterView> ChangeLeaderAsync(Guid factionId, Guid newLeaderActorId, string reason, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken = default);
    Task<FactionMasterView> AssociateCityAsync(Guid factionId, Guid cityId, bool claimCityTerritory, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken = default);
    Task<FactionMasterView> ReleaseCityAsync(Guid factionId, Guid cityId, bool releaseCityTerritory, string reason, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken = default);
    Task<FactionMasterView> ClaimTerritoryAsync(Guid factionId, IReadOnlyCollection<FactionTerritoryPosition> territory, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken = default);
    Task<FactionMasterView> ReleaseTerritoryAsync(Guid factionId, IReadOnlyCollection<FactionTerritoryPosition> territory, string reason, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken = default);
    Task<FactionMasterView> AdjustWealthAsync(Guid factionId, decimal delta, string reason, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken = default);
    Task<FactionMasterView> SetMilitaryPowerAsync(Guid factionId, decimal value, string reason, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken = default);
    Task<FactionMasterView> ApplyRelationModifierAsync(Guid factionId, Guid targetFactionId, FactionRelationModifier modifier, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken = default);
    Task<FactionMasterView> DissolveAsync(Guid factionId, string reason, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken = default);
}
