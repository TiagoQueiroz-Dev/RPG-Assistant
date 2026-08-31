using System.Globalization;
using RpgWorld.Application.Actors.Movement;
using RpgWorld.Application.Realtime;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Simulation.Chunks;
using RpgWorld.Simulation.Engine;

namespace RpgWorld.Simulation.Actors;

public sealed class ActorMovementService(
    IActorMovementStore store,
    IWorldDefinitionCatalog definitions,
    IActorMovementPolicy movementPolicy,
    IChunkActivationService chunkActivationService,
    IWorldCommandGate commandGate,
    IWorldUpdatePublisher publisher,
    TimeProvider timeProvider) : IActorMovementService
{
    public async Task<ActorMoveResult> MoveAsync(
        ActorMoveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ActorId == Guid.Empty) throw new ArgumentException("Actor is required.", nameof(request));
        var worldId = await store.FindActorWorldIdAsync(request.ActorId, cancellationToken)
            ?? throw new KeyNotFoundException("Actor was not found.");
        return await commandGate.ExecuteAsync(worldId, token => MoveInsideGateAsync(request, worldId, token), cancellationToken);
    }

    private async Task<ActorMoveResult> MoveInsideGateAsync(
        ActorMoveRequest request,
        Guid worldId,
        CancellationToken cancellationToken)
    {
        var actor = await store.GetActorAsync(request.ActorId, cancellationToken)
            ?? throw new KeyNotFoundException("Actor was not found.");
        var world = await store.GetWorldAsync(worldId, cancellationToken)
            ?? throw new KeyNotFoundException("Actor world was not found.");
        var destination = world.PositionAt(request.DestinationX, request.DestinationY);
        var origin = actor.Position;
        var originTile = await store.GetTileAsync(origin, cancellationToken)
            ?? throw new InvalidOperationException("Actor origin tile was not found.");
        var destinationTile = await store.GetTileAsync(destination, cancellationToken)
            ?? throw new InvalidOperationException("Actor destination tile was not found.");
        var evaluation = movementPolicy.Evaluate(actor, originTile, destinationTile, definitions);
        var originCoordinate = world.ChunkAt(origin);
        var destinationCoordinate = world.ChunkAt(destination);
        var originChunk = await store.GetChunkAsync(worldId, originCoordinate, cancellationToken)
            ?? throw new InvalidOperationException("Actor origin chunk was not found.");
        var destinationChunk = originCoordinate == destinationCoordinate
            ? originChunk
            : await store.GetChunkAsync(worldId, destinationCoordinate, cancellationToken)
                ?? throw new InvalidOperationException("Actor destination chunk was not found.");
        var occurredAt = timeProvider.GetUtcNow();
        actor.Move(world, destination, occurredAt);
        originTile.RemoveOccupant(actor.Id);
        destinationTile.AddOccupant(actor.Id);
        await store.SaveChangesAsync(cancellationToken);
        await chunkActivationService.ApplyActorMovementAsync(
            worldId,
            actor.Id,
            origin,
            destination,
            cancellationToken);

        var result = new ActorMoveResult(
            actor.Id,
            origin,
            destination,
            originChunk.Id,
            destinationChunk.Id,
            originChunk.Id != destinationChunk.Id,
            evaluation.MovementCost);
        var message = new WorldUpdateMessage(
            Guid.CreateVersion7(),
            worldId,
            "actor.moved",
            occurredAt,
            new Dictionary<string, string?>
            {
                ["actorId"] = actor.Id.ToString(),
                ["actorKind"] = actor.Kind,
                ["originX"] = origin.X.ToString(CultureInfo.InvariantCulture),
                ["originY"] = origin.Y.ToString(CultureInfo.InvariantCulture),
                ["destinationX"] = destination.X.ToString(CultureInfo.InvariantCulture),
                ["destinationY"] = destination.Y.ToString(CultureInfo.InvariantCulture),
                ["movementCost"] = evaluation.MovementCost.ToString(CultureInfo.InvariantCulture)
            });
        await publisher.PublishToChunkAsync(originChunk.Id, message, cancellationToken);
        if (destinationChunk.Id != originChunk.Id)
            await publisher.PublishToChunkAsync(destinationChunk.Id, message, cancellationToken);
        await publisher.PublishToGameMasterAsync(message, cancellationToken);
        return result;
    }
}
