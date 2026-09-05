using RpgWorld.Domain.Actors;

namespace RpgWorld.Application.Actors.Actions;

public interface INpcActionExecutionStore
{
    Task<IReadOnlyList<Guid>> ListCandidatesAsync(Guid worldId, CancellationToken cancellationToken = default);
    Task<NpcActor?> GetAsync(Guid worldId, Guid actorId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    // Roll back all effects and discard tracked changes when the callback fails.
    Task ExecuteAtomicallyAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}
