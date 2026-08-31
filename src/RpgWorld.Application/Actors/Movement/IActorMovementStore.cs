using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Application.Actors.Movement;

public interface IActorMovementStore
{
    Task<Guid?> FindActorWorldIdAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<Actor?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<World?> GetWorldAsync(Guid worldId, CancellationToken cancellationToken = default);
    Task<Tile?> GetTileAsync(Position position, CancellationToken cancellationToken = default);
    Task<Chunk?> GetChunkAsync(Guid worldId, ChunkCoordinate coordinate, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
