using RpgWorld.Domain.Worlds;

namespace RpgWorld.Application.Worlds;

public interface IWorldClockRepository
{
    Task<WorldClock?> GetAsync(Guid worldId, CancellationToken cancellationToken = default);

    Task<bool> WorldExistsAsync(Guid worldId, CancellationToken cancellationToken = default);

    void Add(WorldClock clock);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
