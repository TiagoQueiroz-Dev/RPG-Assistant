using RpgWorld.Domain.Worlds.Cities;

namespace RpgWorld.Application.Worlds.Cities;

public sealed record CityTerritoryPosition(int X, int Y);

public sealed record CreateCityRequest(
    Guid WorldId,
    string Name,
    int CenterX,
    int CenterY,
    IReadOnlyCollection<CityTerritoryPosition> Territory,
    int InitialPopulation,
    decimal InitialWealth,
    DateTimeOffset FoundedAtUtc,
    Guid? GoverningFactionId = null,
    IReadOnlyCollection<Guid>? ResidentActorIds = null);

public sealed record CityMasterView(
    Guid CityId,
    Guid WorldId,
    string Name,
    int CenterX,
    int CenterY,
    string Status,
    int Population,
    decimal Wealth,
    Guid? GoverningFactionId,
    IReadOnlyList<CityTerritoryPosition> Territory,
    IReadOnlyList<Guid> ResidentActorIds,
    IReadOnlyList<Guid> BuildingIds,
    IReadOnlyDictionary<string, decimal> ResourceStocks,
    IReadOnlyDictionary<string, CityResourceMarketSnapshot> ResourceMarkets,
    long EconomicCycleCount,
    DateTimeOffset? LastEconomicCycleAtUtc,
    IReadOnlyList<CityHistoryEntry> History,
    DateTimeOffset FoundedAtUtc,
    DateTimeOffset? DestroyedAtUtc);

public interface ICityService
{
    Task<CityMasterView> CreateAsync(CreateCityRequest request, CancellationToken cancellationToken = default);
    Task<CityMasterView?> GetAsync(Guid cityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CityMasterView>> ListByWorldAsync(Guid worldId, CancellationToken cancellationToken = default);
    Task<CityMasterView> AddResidentAsync(Guid cityId, Guid actorId, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken = default);
    Task<CityMasterView> RemoveResidentAsync(Guid cityId, Guid actorId, string reason, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken = default);
    Task<CityMasterView> ChangePopulationAsync(Guid cityId, int delta, string reason, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken = default);
    Task<CityMasterView> BeginCrisisAsync(Guid cityId, string reason, int severity, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken = default);
    Task<CityMasterView> ResolveCrisisAsync(Guid cityId, string resolution, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken = default);
    Task<CityMasterView> DestroyAsync(Guid cityId, string reason, DateTimeOffset destroyedAtUtc, CancellationToken cancellationToken = default);
}
