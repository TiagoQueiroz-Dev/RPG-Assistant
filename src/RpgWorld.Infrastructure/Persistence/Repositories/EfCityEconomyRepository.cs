using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Worlds.Cities;
using RpgWorld.Domain.Worlds.Cities;
using RpgWorld.Domain.Worlds.Resources;

namespace RpgWorld.Infrastructure.Persistence.Repositories;

public sealed class EfCityEconomyRepository(RpgWorldDbContext dbContext) : ICityEconomyRepository
{
    public async Task<IReadOnlyList<City>> ListSimulatedCitiesAsync(
        Guid worldId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Cities.Include("_territoryTiles")
            .Where(city => city.WorldId == worldId && city.Status != CityStatus.Destroyed)
            .OrderBy(city => city.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ResourceDeposit>> ListAvailableDepositsAsync(
        City city,
        IReadOnlyCollection<string> resourceCodes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(city);
        ArgumentNullException.ThrowIfNull(resourceCodes);
        if (resourceCodes.Count == 0 || city.Territory.Count == 0) return [];
        var world = await dbContext.Worlds.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == city.WorldId, cancellationToken);
        var positions = city.Territory.Select(position => (position.X, position.Y)).ToHashSet();
        var minX = positions.Min(position => position.X);
        var maxX = positions.Max(position => position.X);
        var minY = positions.Min(position => position.Y);
        var maxY = positions.Max(position => position.Y);
        var territoryTileIds = (await dbContext.Tiles.AsNoTracking()
                .Where(tile => tile.WorldId == city.WorldId &&
                    tile.X >= minX && tile.X <= maxX && tile.Y >= minY && tile.Y <= maxY)
                .Select(tile => new { tile.Id, tile.X, tile.Y })
                .ToListAsync(cancellationToken))
            .Where(tile => positions.Contains((tile.X, tile.Y)))
            .Select(tile => tile.Id)
            .ToHashSet();
        var territoryRegions = positions
            .Select(position => (X: position.X / world.ChunkSize, Y: position.Y / world.ChunkSize))
            .ToHashSet();
        var codes = resourceCodes.Distinct(StringComparer.Ordinal).ToArray();
        var candidates = await dbContext.ResourceDeposits
            .Where(deposit => deposit.WorldId == city.WorldId && deposit.IsDiscovered &&
                deposit.Quantity > 0m && codes.Contains(deposit.ResourceCode))
            .OrderBy(deposit => deposit.ResourceCode).ThenBy(deposit => deposit.Id)
            .ToListAsync(cancellationToken);
        return candidates.Where(deposit => deposit.Scope switch
        {
            ResourceDepositScope.Tile => deposit.TileId is { } tileId && territoryTileIds.Contains(tileId),
            ResourceDepositScope.Region => territoryRegions.Contains((deposit.RegionX, deposit.RegionY)),
            _ => false
        }).ToArray();
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
