using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Worlds.Resources;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Resources;

namespace RpgWorld.Infrastructure.Persistence.Repositories;

public sealed class EfNaturalResourceRepository(RpgWorldDbContext dbContext) : INaturalResourceRepository
{
    public Task<World?> GetWorldAsync(Guid worldId, CancellationToken cancellationToken = default) =>
        dbContext.Worlds.SingleOrDefaultAsync(world => world.Id == worldId, cancellationToken);

    public Task<Tile?> GetTileAsync(Position position, CancellationToken cancellationToken = default) =>
        dbContext.Tiles.SingleOrDefaultAsync(
            tile => tile.WorldId == position.WorldId && tile.X == position.X && tile.Y == position.Y,
            cancellationToken);

    public Task<Actor?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) =>
        dbContext.Actors.SingleOrDefaultAsync(actor => actor.Id == actorId, cancellationToken);

    public Task<ResourceDeposit?> GetDepositAsync(Guid depositId, CancellationToken cancellationToken = default) =>
        dbContext.ResourceDeposits.SingleOrDefaultAsync(deposit => deposit.Id == depositId, cancellationToken);

    public async Task<IReadOnlyList<ResourceDeposit>> ListAvailableInRegionAsync(
        Guid worldId,
        ChunkCoordinate region,
        IReadOnlyCollection<string>? resourceCodes = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.ResourceDeposits.Where(deposit =>
            deposit.WorldId == worldId && deposit.RegionX == region.X && deposit.RegionY == region.Y &&
            deposit.IsDiscovered && (deposit.Quantity > 0m || deposit.RegenerationPerWorldHour > 0m));
        if (resourceCodes is { Count: > 0 })
            query = query.Where(deposit => resourceCodes.Contains(deposit.ResourceCode));
        return await query.OrderBy(deposit => deposit.ResourceCode).ThenBy(deposit => deposit.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ResourceDeposit>> ListRegeneratingAsync(
        Guid worldId,
        DateTimeOffset worldInstant,
        CancellationToken cancellationToken = default) =>
        await dbContext.ResourceDeposits.Where(deposit =>
                deposit.WorldId == worldId && deposit.RegenerationPerWorldHour > 0m &&
                deposit.Quantity < deposit.Capacity && deposit.LastRegeneratedAtUtc < worldInstant)
            .OrderBy(deposit => deposit.Id)
            .ToListAsync(cancellationToken);

    public void Add(ResourceDeposit deposit) => dbContext.ResourceDeposits.Add(deposit);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
