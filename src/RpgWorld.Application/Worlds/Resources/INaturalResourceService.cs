using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Resources;

namespace RpgWorld.Application.Worlds.Resources;

public sealed record ResourceSpawnOptions(
    decimal? InitialQuantity = null,
    decimal? Capacity = null,
    decimal? RegenerationPerWorldHour = null,
    Guid? SourceWorldEventId = null);

public sealed record ResourceDepositSnapshot(
    Guid Id,
    Guid WorldId,
    string ResourceCode,
    string InventoryItemCode,
    ResourceDepositScope Scope,
    Guid? TileId,
    int RegionX,
    int RegionY,
    decimal Quantity,
    decimal Capacity,
    decimal RegenerationPerWorldHour,
    bool IsDiscovered,
    bool IsExhausted,
    Guid? SourceWorldEventId);

public interface INaturalResourceService
{
    Task<ResourceDepositSnapshot> SpawnOnTileAsync(
        Guid worldId,
        int x,
        int y,
        string resourceCode,
        DateTimeOffset worldInstant,
        ResourceSpawnOptions? options = null,
        CancellationToken cancellationToken = default);
    Task<ResourceDepositSnapshot> SpawnInRegionAsync(
        Guid worldId,
        ChunkCoordinate region,
        string resourceCode,
        DateTimeOffset worldInstant,
        ResourceSpawnOptions? options = null,
        CancellationToken cancellationToken = default);
    Task<bool> DiscoverAsync(
        Guid depositId,
        Guid actorId,
        DateTimeOffset worldInstant,
        CancellationToken cancellationToken = default);
    Task<ResourceExtraction> CollectForActorAsync(
        Guid depositId,
        Guid actorId,
        int requestedQuantity,
        DateTimeOffset worldInstant,
        CancellationToken cancellationToken = default);
    Task<ResourceExtraction> ConsumeAsync(
        Guid depositId,
        ResourceConsumer consumer,
        decimal requestedQuantity,
        DateTimeOffset worldInstant,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResourceDepositSnapshot>> ListAvailableInRegionAsync(
        Guid worldId,
        Position origin,
        DateTimeOffset worldInstant,
        IReadOnlyCollection<string>? resourceCodes = null,
        CancellationToken cancellationToken = default);
}
