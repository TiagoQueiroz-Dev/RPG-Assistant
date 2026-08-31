using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Actors;
using RpgWorld.Domain.Actors;

namespace RpgWorld.Infrastructure.Persistence.Repositories;

public sealed class EfNpcNeedsRepository(RpgWorldDbContext dbContext) : INpcNeedsRepository
{
    public async Task<IReadOnlyList<NpcActor>> ListForUpdateAsync(
        Guid worldId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Actors.OfType<NpcActor>()
            .Where(npc => npc.WorldId == worldId && npc.Status != ActorStatus.Dead)
            .OrderBy(npc => npc.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<NpcNeedsSnapshot>> ListUrgentAsync(
        Guid worldId,
        decimal minimumHunger,
        decimal maximumEnergy,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (minimumHunger is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(minimumHunger));
        if (maximumEnergy is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(maximumEnergy));
        if (limit is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(limit));
        return await dbContext.Actors.OfType<NpcActor>().AsNoTracking()
            .Where(npc =>
                npc.WorldId == worldId &&
                npc.Status != ActorStatus.Dead &&
                (npc.Hunger >= minimumHunger || npc.Energy <= maximumEnergy))
            .OrderByDescending(npc => npc.Hunger)
            .ThenBy(npc => npc.Energy)
            .ThenBy(npc => npc.Id)
            .Take(limit)
            .Select(npc => new NpcNeedsSnapshot(
                npc.Id,
                npc.WorldId,
                npc.X,
                npc.Y,
                npc.Hunger,
                npc.Energy,
                npc.Money,
                npc.Job,
                npc.FactionId))
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
