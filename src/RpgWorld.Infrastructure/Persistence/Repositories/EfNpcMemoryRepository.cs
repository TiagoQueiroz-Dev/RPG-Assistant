using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Actors.Memories;
using RpgWorld.Domain.Actors.Memories;

namespace RpgWorld.Infrastructure.Persistence.Repositories;

public sealed class EfNpcMemoryRepository(RpgWorldDbContext dbContext) : INpcMemoryRepository
{
    public void Add(NpcMemory memory) => dbContext.NpcMemories.Add(memory);

    public async Task<IReadOnlyList<NpcMemory>> ListAsync(
        Guid actorId,
        Guid? targetId,
        DateTimeOffset asOf,
        int minimumImportance = 1,
        CancellationToken cancellationToken = default)
    {
        ValidateImportance(minimumImportance);
        return await dbContext.NpcMemories.AsNoTracking()
            .Where(memory =>
                memory.ActorId == actorId &&
                (targetId == null || memory.TargetId == targetId) &&
                memory.Importance >= minimumImportance &&
                (memory.ExpiresAt == null || memory.ExpiresAt > asOf.ToUniversalTime()))
            .OrderByDescending(memory => memory.Importance)
            .ThenByDescending(memory => memory.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NpcMemory>> ListRelevantForActorsAsync(
        IReadOnlyCollection<Guid> actorIds,
        DateTimeOffset asOf,
        int minimumImportance,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actorIds);
        ValidateImportance(minimumImportance);
        if (actorIds.Count == 0) return [];
        return await dbContext.NpcMemories.AsNoTracking()
            .Where(memory =>
                actorIds.Contains(memory.ActorId) &&
                memory.Importance >= minimumImportance &&
                (memory.ExpiresAt == null || memory.ExpiresAt > asOf.ToUniversalTime()))
            .OrderBy(memory => memory.ActorId)
            .ThenByDescending(memory => memory.Importance)
            .ToListAsync(cancellationToken);
    }

    public Task<int> DeleteExpiredAsync(
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default) =>
        dbContext.NpcMemories
            .Where(memory => memory.ExpiresAt != null && memory.ExpiresAt <= asOf.ToUniversalTime())
            .ExecuteDeleteAsync(cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SaveChangesAsync(cancellationToken);

    private static void ValidateImportance(int importance)
    {
        if (importance is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(importance));
    }
}
