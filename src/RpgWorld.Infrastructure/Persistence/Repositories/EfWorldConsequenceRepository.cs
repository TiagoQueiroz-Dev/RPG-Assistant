using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Worlds.Events;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds.Events;

namespace RpgWorld.Infrastructure.Persistence.Repositories;

public sealed class EfWorldConsequenceRepository(RpgWorldDbContext dbContext) : IWorldConsequenceRepository
{
    public Task<Actor?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) =>
        dbContext.Actors.SingleOrDefaultAsync(actor => actor.Id == actorId, cancellationToken);

    public async Task<int> CountLivingFamilyAsync(
        Guid worldId,
        Guid victimId,
        CancellationToken cancellationToken = default) =>
        (await dbContext.Actors.AsNoTracking().OfType<NpcActor>()
            .Where(actor => actor.WorldId == worldId && actor.Status != ActorStatus.Dead)
            .ToListAsync(cancellationToken))
        .Count(actor => actor.FamilyIds.Contains(victimId));

    public Task<bool> ExistsAsync(
        Guid sourceEventId,
        WorldConsequenceKind kind,
        Guid targetId,
        CancellationToken cancellationToken = default) =>
        dbContext.WorldConsequences.AnyAsync(value =>
            value.SourceEventId == sourceEventId && value.Kind == kind && value.TargetId == targetId,
            cancellationToken);

    public void Add(WorldConsequence consequence) => dbContext.WorldConsequences.Add(consequence);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
