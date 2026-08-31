using RpgWorld.Application.Caching;
using RpgWorld.Application.Events;
using RpgWorld.Application.Worlds;
using RpgWorld.Domain.Events;
using RpgWorld.Domain.Worlds;
using RpgWorld.Simulation.Regions;

namespace RpgWorld.Simulation.Chunks;

public sealed class ChunkActivationService(
    IWorldMapRepository repository,
    ICacheService cache,
    IDomainEventDispatcher eventDispatcher,
    ChunkActivationOptions options,
    TimeProvider timeProvider,
    IRegionSimulationService? regionSimulationService = null,
    ActiveChunkRegistry? activeChunkRegistry = null) : IChunkActivationService, IDisposable
{
    private readonly ActiveChunkRegistry _registry = activeChunkRegistry ?? new ActiveChunkRegistry();
    private readonly bool _ownsRegistry = activeChunkRegistry is null;

    public IReadOnlyCollection<ActiveChunk> GetActiveChunks(Guid worldId) =>
        _registry.Chunks
            .Where(pair => pair.Key.WorldId == worldId)
            .Select(pair => pair.Value)
            .OrderBy(active => active.Chunk.Coordinate.Y)
            .ThenBy(active => active.Chunk.Coordinate.X)
            .ToArray();

    public bool TryGetActiveChunk(
        Guid worldId,
        ChunkCoordinate coordinate,
        out ActiveChunk? activeChunk) =>
        _registry.Chunks.TryGetValue(new ActiveChunkKey(worldId, coordinate), out activeChunk);

    public async Task ApplyActorMovementAsync(
        Guid worldId,
        Guid actorId,
        Position origin,
        Position destination,
        CancellationToken cancellationToken = default)
    {
        if (origin.WorldId != worldId || destination.WorldId != worldId)
            throw new ArgumentException("Movement positions must belong to the world.");
        await _registry.Gate.WaitAsync(cancellationToken);
        try
        {
            foreach (var activeChunk in _registry.Chunks.Values.Where(active => active.Chunk.WorldId == worldId))
            {
                if (activeChunk.Chunk.Contains(origin))
                    activeChunk.Tiles.SingleOrDefault(tile => tile.Position == origin)?.RemoveOccupant(actorId);
                if (activeChunk.Chunk.Contains(destination))
                    activeChunk.Tiles.SingleOrDefault(tile => tile.Position == destination)?.AddOccupant(actorId);
            }
        }
        finally { _registry.Gate.Release(); }
    }

    public async Task SynchronizeAsync(
        World world,
        IEnumerable<Position> playerPositions,
        IEnumerable<ChunkCoordinate>? relevantRegions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(playerPositions);

        var players = playerPositions.ToArray();
        var relevant = (relevantRegions ?? []).ToArray();
        var requiredCoordinates = ResolveRequiredCoordinates(
            world,
            players,
            relevant);
        if (regionSimulationService is not null)
        {
            await regionSimulationService.SynchronizeAsync(
                world,
                players,
                relevant,
                cancellationToken);
        }
        var now = timeProvider.GetUtcNow();
        var events = new List<IDomainEvent>();

        await _registry.Gate.WaitAsync(cancellationToken);

        try
        {
            foreach (var coordinate in requiredCoordinates)
            {
                var key = new ActiveChunkKey(world.Id, coordinate);

                if (_registry.Chunks.TryGetValue(key, out var existing))
                {
                    existing.MarkRelevant(now);
                    await CacheAsync(existing, cancellationToken);
                    continue;
                }

                var chunk = await repository.GetChunkAsync(world.Id, coordinate, cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Chunk ({coordinate.X}, {coordinate.Y}) does not exist in world '{world.Id}'.");
                var tiles = await repository.GetTilesAsync(world.Id, coordinate, cancellationToken);
                var activeChunk = new ActiveChunk(chunk, tiles, now);

                if (_registry.Chunks.TryAdd(key, activeChunk))
                {
                    await CacheAsync(activeChunk, cancellationToken);
                    events.Add(new ChunkActivatedEvent(
                        world.Id,
                        coordinate.X,
                        coordinate.Y,
                        tiles.Count,
                        now));
                }
            }

            var unloadCandidates = _registry.Chunks
                .Where(pair =>
                    pair.Key.WorldId == world.Id &&
                    !requiredCoordinates.Contains(pair.Key.Coordinate) &&
                    now - pair.Value.LastRelevantAtUtc >= options.InactivityTimeout)
                .ToArray();

            foreach (var candidate in unloadCandidates)
            {
                var activeChunk = candidate.Value;
                await repository.PersistAndReleaseChunkAsync(
                    activeChunk.Chunk,
                    activeChunk.Tiles,
                    cancellationToken);
                await cache.RemoveAsync(
                    CacheKeys.ActiveChunk(
                        world.Id,
                        candidate.Key.Coordinate.X,
                        candidate.Key.Coordinate.Y),
                    cancellationToken);

                if (_registry.Chunks.TryRemove(candidate.Key, out _))
                {
                    events.Add(new ChunkDeactivatedEvent(
                        world.Id,
                        candidate.Key.Coordinate.X,
                        candidate.Key.Coordinate.Y,
                        now - activeChunk.LastRelevantAtUtc,
                        now));
                }
            }
        }
        finally
        {
            _registry.Gate.Release();
        }

        if (events.Count > 0)
        {
            await eventDispatcher.DispatchAsync(events, cancellationToken);
        }
    }

    public void Dispose()
    {
        if (_ownsRegistry) _registry.Dispose();
    }

    private HashSet<ChunkCoordinate> ResolveRequiredCoordinates(
        World world,
        IEnumerable<Position> playerPositions,
        IEnumerable<ChunkCoordinate> relevantRegions)
    {
        var required = new HashSet<ChunkCoordinate>();

        foreach (var position in playerPositions)
        {
            var center = world.ChunkAt(position);

            for (var offsetY = -options.PlayerRadius; offsetY <= options.PlayerRadius; offsetY++)
            {
                for (var offsetX = -options.PlayerRadius; offsetX <= options.PlayerRadius; offsetX++)
                {
                    var x = center.X + offsetX;
                    var y = center.Y + offsetY;

                    if (x >= 0 && x < world.ChunkColumns && y >= 0 && y < world.ChunkRows)
                    {
                        required.Add(new ChunkCoordinate(x, y));
                    }
                }
            }
        }

        foreach (var coordinate in relevantRegions)
        {
            if (coordinate.X >= world.ChunkColumns || coordinate.Y >= world.ChunkRows)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(relevantRegions),
                    $"Relevant chunk ({coordinate.X}, {coordinate.Y}) is outside the world.");
            }

            required.Add(coordinate);
        }

        return required;
    }

    private Task CacheAsync(
        ActiveChunk activeChunk,
        CancellationToken cancellationToken) =>
        cache.SetAsync(
            CacheKeys.ActiveChunk(
                activeChunk.Chunk.WorldId,
                activeChunk.Chunk.Coordinate.X,
                activeChunk.Chunk.Coordinate.Y),
            new ActiveChunkCacheEntry(
                activeChunk.Chunk.WorldId,
                activeChunk.Chunk.Coordinate.X,
                activeChunk.Chunk.Coordinate.Y,
                activeChunk.Tiles.Count,
                activeChunk.ActivatedAtUtc,
                activeChunk.LastRelevantAtUtc),
            CachePolicy.For(CacheDataKind.ActiveChunk),
            cancellationToken);

}
