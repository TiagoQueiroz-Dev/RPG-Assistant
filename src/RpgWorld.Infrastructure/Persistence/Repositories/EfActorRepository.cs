using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Actors;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Infrastructure.Persistence.Repositories;

public sealed class EfActorRepository(RpgWorldDbContext dbContext) : IActorRepository
{
    public Task<Actor?> GetAsync(Guid actorId, CancellationToken cancellationToken = default) =>
        dbContext.Actors.SingleOrDefaultAsync(actor => actor.Id == actorId, cancellationToken);

    public async Task<IReadOnlyList<Actor>> ListByWorldAsync(
        Guid worldId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Actors.AsNoTracking()
            .Where(actor => actor.WorldId == worldId)
            .OrderBy(actor => actor.Name)
            .ThenBy(actor => actor.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Actor>> ListAtPositionAsync(
        Position position,
        CancellationToken cancellationToken = default) =>
        await dbContext.Actors.AsNoTracking()
            .Where(actor => actor.WorldId == position.WorldId && actor.X == position.X && actor.Y == position.Y)
            .OrderBy(actor => actor.Id)
            .ToListAsync(cancellationToken);

    public void Add(Actor actor) => dbContext.Actors.Add(actor);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
