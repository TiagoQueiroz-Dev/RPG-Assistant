using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Worlds.Events;
using RpgWorld.Domain.Worlds.Events;

namespace RpgWorld.Infrastructure.Persistence.Repositories;

public sealed class EfWorldEventRepository(RpgWorldDbContext dbContext) : IWorldEventRepository
{
    public Task<bool> WorldExistsAsync(Guid worldId, CancellationToken cancellationToken = default) =>
        dbContext.Worlds.AnyAsync(world => world.Id == worldId, cancellationToken);

    public async Task<WorldEventPage> SearchAsync(
        WorldEventQuery query,
        CancellationToken cancellationToken = default)
    {
        var events = dbContext.WorldEvents.AsNoTracking().Where(worldEvent => worldEvent.WorldId == query.WorldId);
        if (query.Type is { } type) events = events.Where(worldEvent => worldEvent.Type == type);
        if (query.ActorId is { } actorId)
            events = events.Where(worldEvent => EF.Property<List<Guid>>(worldEvent, "_actorIds").Contains(actorId));
        if (query.FromUtc is { } from) events = events.Where(worldEvent => worldEvent.TimestampUtc >= from.ToUniversalTime());
        if (query.ToUtc is { } to) events = events.Where(worldEvent => worldEvent.TimestampUtc <= to.ToUniversalTime());
        if (query.PositionX is { } x && query.PositionY is { } y)
            events = events.Where(worldEvent => worldEvent.PositionX == x && worldEvent.PositionY == y);
        if (query.CorrelationId is { } correlationId)
            events = events.Where(worldEvent => worldEvent.CorrelationId == correlationId);
        var totalCount = await events.LongCountAsync(cancellationToken);
        events = query.SortOrder == WorldEventSortOrder.OldestFirst
            ? events.OrderBy(worldEvent => worldEvent.TimestampUtc).ThenBy(worldEvent => worldEvent.Id)
            : events.OrderByDescending(worldEvent => worldEvent.TimestampUtc).ThenByDescending(worldEvent => worldEvent.Id);
        var items = await events.Skip(checked((query.Page - 1) * query.PageSize)).Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return new WorldEventPage(items, query.Page, query.PageSize, totalCount);
    }
}
