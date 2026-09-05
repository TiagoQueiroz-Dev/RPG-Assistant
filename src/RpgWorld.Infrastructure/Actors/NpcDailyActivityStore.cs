using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Actors.Actions;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Actors.Housing;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Cities;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Infrastructure.Persistence;

namespace RpgWorld.Infrastructure.Actors;

public sealed class NpcDailyActivityStore(RpgWorldDbContext db, IWorldDefinitionCatalog definitions) : INpcDailyActivityStore
{
    public async Task<NpcFoodSource?> FindFoodAsync(NpcActor npc, CancellationToken cancellationToken = default)
    {
        var source = await (from deposit in db.ResourceDeposits
            join tile in db.Tiles on deposit.TileId equals tile.Id
            where deposit.WorldId == npc.WorldId && deposit.IsDiscovered && deposit.Quantity >= 1m &&
                deposit.InventoryItemCode == "food"
            orderby Math.Abs(tile.X - npc.X) + Math.Abs(tile.Y - npc.Y), tile.Y, tile.X
            select new { Deposit = deposit, tile.X, tile.Y }).FirstOrDefaultAsync(cancellationToken);
        return source is null ? null : new(source.Deposit, new(npc.WorldId, source.X, source.Y));
    }

    public Task<City?> GetWorkCityAsync(NpcActor npc, CancellationToken cancellationToken = default) =>
        db.Cities.SingleOrDefaultAsync(city => city.WorldId == npc.WorldId && city.Id == npc.ResidentCityId &&
            city.Status != CityStatus.Destroyed, cancellationToken);

    public async Task<bool> CanRestAsync(NpcActor npc, Position position, CancellationToken cancellationToken = default)
    {
        if (position.WorldId != npc.WorldId) return false;
        var tile = await db.Tiles.AsNoTracking().SingleOrDefaultAsync(tile => tile.WorldId == npc.WorldId &&
            tile.X == position.X && tile.Y == position.Y, cancellationToken);
        if (tile is null || !definitions.ResolveTerrain(tile.TerrainCode).IsTraversable) return false;
        if (npc.HomeStructureId is { } houseId && npc.Home == position)
            return await db.HousingConstructions.AnyAsync(house => house.Id == houseId && house.OwnerActorId == npc.Id &&
                house.Status == HousingConstructionStatus.Completed, cancellationToken);
        return true;
    }
}
