using RpgWorld.Domain.Actors.Memories;

namespace RpgWorld.Application.Actors.Memories;

public interface INpcMemoryRepository
{
    void Add(NpcMemory memory);

    Task<IReadOnlyList<NpcMemory>> ListAsync(
        Guid actorId,
        Guid? targetId,
        DateTimeOffset asOf,
        int minimumImportance = 1,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NpcMemory>> ListRelevantForActorsAsync(
        IReadOnlyCollection<Guid> actorIds,
        DateTimeOffset asOf,
        int minimumImportance,
        CancellationToken cancellationToken = default);

    Task<int> DeleteExpiredAsync(
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
