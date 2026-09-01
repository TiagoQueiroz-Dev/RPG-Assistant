using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Worlds.Visibility;
using RpgWorld.Domain.Actors;
using RpgWorld.Infrastructure.Persistence;

namespace RpgWorld.Infrastructure.Worlds.Visibility;

public sealed class PlayerWorldViewService(
    RpgWorldDbContext dbContext,
    IPlayerVisibilityService visibilityService) : IPlayerWorldViewService
{
    public async Task<PlayerWorldView> GetAsync(
        Guid playerActorId,
        CancellationToken cancellationToken = default)
    {
        var visibility = await visibilityService.GetAsync(playerActorId, cancellationToken);
        var player = await dbContext.Actors.AsNoTracking().OfType<PlayerActor>()
            .SingleAsync(value => value.Id == playerActorId, cancellationToken);
        var world = await dbContext.Worlds.AsNoTracking()
            .SingleAsync(value => value.Id == player.WorldId, cancellationToken);
        var visiblePositions = visibility.Tiles.Where(value => value.State == nameof(PlayerKnowledgeState.Visible))
            .Select(value => (value.X, value.Y)).ToHashSet();
        var xValues = visiblePositions.Select(value => value.X).Distinct().ToArray();
        var yValues = visiblePositions.Select(value => value.Y).Distinct().ToArray();
        var actors = await dbContext.Actors.AsNoTracking().Where(value => value.WorldId == player.WorldId &&
            value.Id != player.Id && value.Status != ActorStatus.Dead && xValues.Contains(value.X) && yValues.Contains(value.Y))
            .ToArrayAsync(cancellationToken);
        var visibleActors = actors.Where(value => visiblePositions.Contains((value.X, value.Y)))
            .OrderBy(value => Distance(player.X, player.Y, value.X, value.Y)).ThenBy(value => value.Name)
            .Select(value => new PlayerVisibleEntityView(
                value.Id, value.Name, value.Kind, value.X, value.Y, Distance(player.X, player.Y, value.X, value.Y)))
            .ToArray();
        var housing = await dbContext.HousingConstructions.AsNoTracking().Where(value => value.WorldId == player.WorldId &&
            xValues.Contains(value.X) && yValues.Contains(value.Y)).ToArrayAsync(cancellationToken);
        var visibleHousing = housing.Where(value => visiblePositions.Contains((value.X, value.Y))).ToArray();
        var visibleHousingIds = visibleHousing.Select(value => value.Id).ToHashSet();
        var structureTiles = await dbContext.Tiles.AsNoTracking().Where(value => value.WorldId == player.WorldId &&
            value.StructureId != null && xValues.Contains(value.X) && yValues.Contains(value.Y))
            .Select(value => new { value.StructureId, value.X, value.Y }).ToArrayAsync(cancellationToken);
        var structures = visibleHousing.Select(value => new PlayerVisibleStructureView(
                value.Id, "housing", value.X, value.Y))
            .Concat(structureTiles.Where(value => visiblePositions.Contains((value.X, value.Y)) &&
                    !visibleHousingIds.Contains(value.StructureId!.Value))
                .Select(value => new PlayerVisibleStructureView(value.StructureId!.Value, "building", value.X, value.Y)))
            .OrderBy(value => value.Y).ThenBy(value => value.X).ToArray();
        var recentEvents = await dbContext.WorldEvents.AsNoTracking().Where(value => value.WorldId == player.WorldId)
            .OrderByDescending(value => value.TimestampUtc).ThenByDescending(value => value.Id)
            .Take(200).ToArrayAsync(cancellationToken);
        var visibleActorIds = visibleActors.Select(value => value.Id).Append(player.Id).ToHashSet();
        var relevantEvents = recentEvents.Where(value => value.Position is { } position
                ? visiblePositions.Contains((position.X, position.Y))
                : value.ActorIds.Any(visibleActorIds.Contains))
            .Take(50)
            .Select(value => new PlayerVisibleEventView(
                value.Id, value.Type, value.TimestampUtc, value.PositionX, value.PositionY))
            .ToArray();
        return new PlayerWorldView(
            player.Id,
            world.Id,
            world.Name,
            player.Name,
            player.X,
            player.Y,
            visibility.PerceptionRadius,
            visibleActors,
            structures,
            relevantEvents);
    }

    private static int Distance(int originX, int originY, int x, int y) =>
        Math.Max(Math.Abs(originX - x), Math.Abs(originY - y));
}
