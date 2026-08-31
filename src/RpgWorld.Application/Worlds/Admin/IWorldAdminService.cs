namespace RpgWorld.Application.Worlds.Admin;

public sealed record WorldAdminQuery(
    Guid WorldId,
    string EntityType = "chunks",
    int Page = 1,
    int PageSize = 50,
    int? RegionX = null,
    int? RegionY = null,
    Guid? FactionId = null);

public sealed record WorldAdminMapView(
    int Width,
    int Height,
    int ChunkSize,
    long TotalTiles,
    int TotalChunks);

public sealed record WorldAdminSummary(
    int TotalActors,
    int Npcs,
    int Players,
    int Creatures,
    int ActiveChunks,
    int ResourceDeposits,
    decimal AvailableResourceQuantity,
    int Cities,
    int TotalPopulation,
    decimal CityWealth,
    int Factions,
    int Armies,
    decimal MilitaryPower,
    int DiplomaticRelations,
    int ActiveWars);

public sealed record WorldAdminEntityView(
    Guid Id,
    string EntityType,
    string Name,
    string Status,
    int? X,
    int? Y,
    int? RegionX,
    int? RegionY,
    Guid? FactionId,
    string? DetailPath,
    IReadOnlyDictionary<string, string> Metrics);

public sealed record WorldAdminView(
    Guid WorldId,
    string Name,
    bool IsSimulationRunning,
    DateTimeOffset? CurrentInstant,
    WorldAdminMapView Map,
    WorldAdminSummary Summary,
    string EntityType,
    IReadOnlyList<WorldAdminEntityView> Entities,
    int Page,
    int PageSize,
    long TotalEntityCount,
    int TotalPages,
    IReadOnlyList<string> AvailableEntityTypes);

public interface IWorldAdminRepository
{
    Task<WorldAdminView?> InspectAsync(WorldAdminQuery query, CancellationToken cancellationToken = default);
}

public interface IWorldAdminService
{
    Task<WorldAdminView> InspectAsync(WorldAdminQuery query, CancellationToken cancellationToken = default);
}
