using RpgWorld.Domain.Worlds.Factions;

namespace RpgWorld.Application.Worlds.Factions;

public sealed record FactionWarContext(
    int SharedBorderEdges,
    int SourceCriticalShortageMarkets,
    decimal TargetStoredResources,
    bool AggressiveLeader);

public interface IFactionWarRepository
{
    Task<IReadOnlyList<Faction>> ListActiveAsync(Guid worldId, CancellationToken cancellationToken = default);
    Task<FactionWarContext> BuildContextAsync(
        Faction source,
        Faction target,
        CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
