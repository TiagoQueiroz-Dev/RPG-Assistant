using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Resources;

namespace RpgWorld.Application.Worlds.Resources;

public interface INaturalResourceRepository
{
    Task<World?> GetWorldAsync(Guid worldId, CancellationToken cancellationToken = default);
    Task<Tile?> GetTileAsync(Position position, CancellationToken cancellationToken = default);
    Task<Actor?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<ResourceDeposit?> GetDepositAsync(Guid depositId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResourceDeposit>> ListAvailableInRegionAsync(
        Guid worldId,
        ChunkCoordinate region,
        IReadOnlyCollection<string>? resourceCodes = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResourceDeposit>> ListRegeneratingAsync(
        Guid worldId,
        DateTimeOffset worldInstant,
        CancellationToken cancellationToken = default);
    void Add(ResourceDeposit deposit);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
