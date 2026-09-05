using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Actors.Actions;

namespace RpgWorld.Application.Actors.Actions;

public interface INpcTravelDestinationResolver
{
    Task<NpcActionTarget?> ResolveAsync(NpcActor npc, CancellationToken cancellationToken = default);
}
