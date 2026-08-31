using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds.Events;

namespace RpgWorld.Application.Worlds.Events;

public interface IWorldConsequenceRepository
{
    Task<Actor?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<int> CountLivingFamilyAsync(Guid worldId, Guid victimId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(
        Guid sourceEventId,
        WorldConsequenceKind kind,
        Guid targetId,
        CancellationToken cancellationToken = default);
    void Add(WorldConsequence consequence);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
