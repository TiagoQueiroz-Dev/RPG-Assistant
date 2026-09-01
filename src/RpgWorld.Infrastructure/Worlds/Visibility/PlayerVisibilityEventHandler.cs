using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Events;
using RpgWorld.Application.Worlds.Visibility;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Events;
using RpgWorld.Infrastructure.Persistence;

namespace RpgWorld.Infrastructure.Worlds.Visibility;

public sealed class PlayerVisibilityCreatedEventHandler(
    IPlayerVisibilityService visibilityService) : IDomainEventHandler<ActorCreatedEvent>
{
    public Task HandleAsync(ActorCreatedEvent domainEvent, CancellationToken cancellationToken = default) =>
        string.Equals(domainEvent.ActorKind, "player", StringComparison.Ordinal)
            ? visibilityService.RefreshAsync(domainEvent.ActorId, domainEvent.OccurredAtUtc, cancellationToken)
            : Task.CompletedTask;

}

public sealed class PlayerVisibilityMovedEventHandler(
    RpgWorldDbContext dbContext,
    IPlayerVisibilityService visibilityService) : IDomainEventHandler<ActorMovedEvent>
{
    public async Task HandleAsync(ActorMovedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        if (await dbContext.Actors.AsNoTracking().OfType<PlayerActor>()
            .AnyAsync(value => value.Id == domainEvent.ActorId, cancellationToken))
            await visibilityService.RefreshAsync(domainEvent.ActorId, domainEvent.OccurredAtUtc, cancellationToken);
    }
}
