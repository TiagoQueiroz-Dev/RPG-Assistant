using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Application.Actors;

public interface IActorRepository
{
    Task<Actor?> GetAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Actor>> ListByWorldAsync(Guid worldId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Actor>> ListAtPositionAsync(Position position, CancellationToken cancellationToken = default);
    void Add(Actor actor);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
