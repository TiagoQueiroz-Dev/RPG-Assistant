using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Worlds;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Infrastructure.Persistence.Repositories;

public sealed class EfWorldSimulationRepository(RpgWorldDbContext dbContext)
    : IWorldSimulationRepository
{
    public async Task<IReadOnlyList<Guid>> ListRunningWorldIdsAsync(
        CancellationToken cancellationToken = default) =>
        await dbContext.Worlds
            .AsNoTracking()
            .Where(world => world.IsSimulationRunning)
            .OrderBy(world => world.Id)
            .Select(world => world.Id)
            .ToListAsync(cancellationToken);

    public Task<World?> GetAsync(
        Guid worldId,
        CancellationToken cancellationToken = default) =>
        dbContext.Worlds.SingleOrDefaultAsync(
            world => world.Id == worldId,
            cancellationToken);

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
