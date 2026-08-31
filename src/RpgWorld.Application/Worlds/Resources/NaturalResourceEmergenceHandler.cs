using RpgWorld.Application.Events;
using RpgWorld.Domain.Events;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Resources;

namespace RpgWorld.Application.Worlds.Resources;

public sealed class NaturalResourceEmergenceHandler(INaturalResourceService service)
    : IDomainEventHandler<NaturalResourceEmergenceEvent>
{
    public async Task HandleAsync(
        NaturalResourceEmergenceEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        var options = new ResourceSpawnOptions(
            domainEvent.InitialQuantity,
            domainEvent.Capacity,
            domainEvent.RegenerationPerWorldHour,
            domainEvent.EventId);
        if (domainEvent.Scope == ResourceDepositScope.Tile)
        {
            await service.SpawnOnTileAsync(
                domainEvent.WorldId,
                domainEvent.X,
                domainEvent.Y,
                domainEvent.ResourceCode,
                domainEvent.OccurredAtUtc,
                options,
                cancellationToken);
            return;
        }

        await service.SpawnInRegionAsync(
            domainEvent.WorldId,
            new ChunkCoordinate(domainEvent.X, domainEvent.Y),
            domainEvent.ResourceCode,
            domainEvent.OccurredAtUtc,
            options,
            cancellationToken);
    }
}
