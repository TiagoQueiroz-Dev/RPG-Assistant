using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Worlds;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Infrastructure.Persistence.Repositories;

public sealed class EfWorldClockRepository(RpgWorldDbContext dbContext) : IWorldClockRepository
{
    public Task<WorldClock?> GetAsync(Guid worldId, CancellationToken cancellationToken = default) =>
        dbContext.WorldClocks.SingleOrDefaultAsync(clock => clock.WorldId == worldId, cancellationToken);

    public Task<bool> WorldExistsAsync(Guid worldId, CancellationToken cancellationToken = default) =>
        dbContext.Worlds.AnyAsync(world => world.Id == worldId, cancellationToken);

    public void Add(WorldClock clock) => dbContext.WorldClocks.Add(clock);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
