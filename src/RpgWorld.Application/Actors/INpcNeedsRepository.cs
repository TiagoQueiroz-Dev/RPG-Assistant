using RpgWorld.Domain.Actors;

namespace RpgWorld.Application.Actors;

public interface INpcNeedsRepository
{
    Task<IReadOnlyList<NpcActor>> ListForUpdateAsync(Guid worldId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NpcNeedsSnapshot>> ListUrgentAsync(
        Guid worldId,
        decimal minimumHunger,
        decimal maximumEnergy,
        int limit = 100,
        CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
