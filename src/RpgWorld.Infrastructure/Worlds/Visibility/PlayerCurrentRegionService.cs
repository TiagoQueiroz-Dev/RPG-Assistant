using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Worlds.Visibility;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds.Cities;
using RpgWorld.Infrastructure.Persistence;

namespace RpgWorld.Infrastructure.Worlds.Visibility;

public sealed class PlayerCurrentRegionService(
    RpgWorldDbContext dbContext,
    IPlayerWorldViewService worldViewService) : IPlayerCurrentRegionService
{
    public async Task<PlayerCurrentRegionView> GetAsync(
        Guid playerActorId,
        CancellationToken cancellationToken = default)
    {
        var view = await worldViewService.GetAsync(playerActorId, cancellationToken);
        var region = await ResolveRegionAsync(view, cancellationToken);
        var entityIds = view.VisibleEntities.Where(value => region.Contains(value.X, value.Y))
            .Select(value => value.Id).ToArray();
        var npcJobs = await dbContext.Actors.AsNoTracking().OfType<NpcActor>()
            .Where(value => entityIds.Contains(value.Id))
            .Select(value => new { value.Id, value.Job })
            .ToDictionaryAsync(value => value.Id, value => value.Job, cancellationToken);
        var entities = view.VisibleEntities.Where(value => region.Contains(value.X, value.Y))
            .Select(value => Entity(value, npcJobs.GetValueOrDefault(value.Id)))
            .OrderByDescending(value => value.Relevance)
            .ThenBy(value => value.Distance)
            .ThenBy(value => value.Name)
            .ToArray();
        var establishments = view.VisibleStructures.Where(value => region.Contains(value.X, value.Y))
            .OrderBy(value => Distance(view.X, view.Y, value.X, value.Y))
            .ToArray();
        var events = view.RelevantEvents.Where(value =>
                value.X is not { } x || value.Y is not { } y || region.Contains(x, y))
            .ToArray();
        return new PlayerCurrentRegionView(
            view.PlayerActorId, view.WorldId, view.WorldName, view.CharacterName,
            region.Id, region.Kind, region.Name, view.X, view.Y, view.PerceptionRadius,
            entities, establishments, events);
    }

    private async Task<Region> ResolveRegionAsync(PlayerWorldView view, CancellationToken cancellationToken)
    {
        var territory = await dbContext.CityTerritoryTiles.AsNoTracking()
            .SingleOrDefaultAsync(value => value.WorldId == view.WorldId && value.X == view.X &&
                value.Y == view.Y && value.IsActive, cancellationToken);
        if (territory is not null)
        {
            var city = await dbContext.Cities.AsNoTracking().SingleAsync(value => value.Id == territory.CityId, cancellationToken);
            if (city.Status != CityStatus.Destroyed)
            {
                var positions = await dbContext.CityTerritoryTiles.AsNoTracking()
                    .Where(value => value.CityId == city.Id && value.IsActive)
                    .Select(value => new { value.X, value.Y }).ToArrayAsync(cancellationToken);
                var coordinates = positions.Select(value => (value.X, value.Y)).ToHashSet();
                return new Region(city.Id, "city", city.Name, (x, y) => coordinates.Contains((x, y)));
            }
        }
        var chunk = await dbContext.Chunks.AsNoTracking().SingleAsync(value => value.WorldId == view.WorldId &&
            view.X >= value.OriginX && view.X < value.OriginX + value.Width &&
            view.Y >= value.OriginY && view.Y < value.OriginY + value.Height, cancellationToken);
        return new Region(chunk.Id, "chunk", $"Region {chunk.CoordinateX},{chunk.CoordinateY}",
            (x, y) => x >= chunk.OriginX && x < chunk.OriginX + chunk.Width &&
                y >= chunk.OriginY && y < chunk.OriginY + chunk.Height);
    }

    private static PlayerRegionEntityView Entity(PlayerVisibleEntityView entity, string? job)
    {
        var category = Category(entity.Kind, job);
        var relevance = category switch { "guard" => 4, "merchant" => 3, "player" => 2, _ => 1 };
        return new PlayerRegionEntityView(entity.Id, entity.Name, entity.Kind, category,
            entity.X, entity.Y, entity.Distance, relevance);
    }

    private static string Category(string kind, string? job)
    {
        if (kind is "player" or "creature") return kind;
        if (Contains(job, "guard", "watch", "soldier")) return "guard";
        if (Contains(job, "merchant", "trader", "shopkeeper", "vendor")) return "merchant";
        return "npc";
    }

    private static bool Contains(string? value, params string[] candidates) =>
        value is not null && candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));

    private static int Distance(int originX, int originY, int x, int y) =>
        Math.Max(Math.Abs(originX - x), Math.Abs(originY - y));

    private sealed record Region(Guid Id, string Kind, string Name, Func<int, int, bool> Contains);
}
