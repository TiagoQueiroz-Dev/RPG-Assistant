using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Actors.Movement;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Infrastructure.Persistence.Repositories;

public sealed class EfActorMovementStore(RpgWorldDbContext dbContext) : IActorMovementStore
{
    public async Task<Guid?> FindActorWorldIdAsync(
        Guid actorId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Actors.AsNoTracking()
            .Where(actor => actor.Id == actorId)
            .Select(actor => (Guid?)actor.WorldId)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Actor?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) =>
        dbContext.Actors.SingleOrDefaultAsync(actor => actor.Id == actorId, cancellationToken);

    public Task<World?> GetWorldAsync(Guid worldId, CancellationToken cancellationToken = default) =>
        dbContext.Worlds.SingleOrDefaultAsync(world => world.Id == worldId, cancellationToken);

    public Task<Tile?> GetTileAsync(Position position, CancellationToken cancellationToken = default) =>
        dbContext.Tiles.SingleOrDefaultAsync(tile =>
            tile.WorldId == position.WorldId && tile.X == position.X && tile.Y == position.Y,
            cancellationToken);

    public Task<Chunk?> GetChunkAsync(
        Guid worldId,
        ChunkCoordinate coordinate,
        CancellationToken cancellationToken = default) =>
        dbContext.Chunks.SingleOrDefaultAsync(chunk =>
            chunk.WorldId == worldId &&
            chunk.CoordinateX == coordinate.X &&
            chunk.CoordinateY == coordinate.Y,
            cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
