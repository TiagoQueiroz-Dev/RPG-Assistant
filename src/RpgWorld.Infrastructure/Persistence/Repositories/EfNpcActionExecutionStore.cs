using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Actors.Actions;
using RpgWorld.Domain.Actors;

namespace RpgWorld.Infrastructure.Persistence.Repositories;

public sealed class EfNpcActionExecutionStore(RpgWorldDbContext dbContext) : INpcActionExecutionStore
{
    public async Task<IReadOnlyList<Guid>> ListCandidatesAsync(Guid worldId, CancellationToken cancellationToken = default) =>
        await dbContext.Actors.AsNoTracking().OfType<NpcActor>()
            .Where(npc => npc.WorldId == worldId && npc.Status != ActorStatus.Dead && npc.CurrentAction != null)
            .OrderBy(npc => npc.Id).Select(npc => npc.Id).ToArrayAsync(cancellationToken);

    public Task<NpcActor?> GetAsync(Guid worldId, Guid actorId, CancellationToken cancellationToken = default) =>
        dbContext.Actors.OfType<NpcActor>().SingleOrDefaultAsync(
            npc => npc.WorldId == worldId && npc.Id == actorId, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SaveChangesAsync(cancellationToken);

    public async Task ExecuteAtomicallyAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            dbContext.Effects.Begin();
            try
            {
                await action(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                try { await transaction.RollbackAsync(CancellationToken.None); }
                finally { dbContext.ChangeTracker.Clear(); dbContext.Effects.Discard(); }
                throw;
            }
        });
        await dbContext.Effects.FlushAsync();
    }
}
