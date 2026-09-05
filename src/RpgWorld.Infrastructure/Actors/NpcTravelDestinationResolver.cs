using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Actors.Actions;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Actors.Actions;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Cities;
using RpgWorld.Infrastructure.Persistence;

namespace RpgWorld.Infrastructure.Actors;

public sealed class NpcTravelDestinationResolver(RpgWorldDbContext dbContext) : INpcTravelDestinationResolver
{
    public async Task<NpcActionTarget?> ResolveAsync(NpcActor npc, CancellationToken cancellationToken = default)
    {
        if (npc.ActionExecution?.Target is { Position: not null } target) return target;
        var targetId = npc.ActionExecution?.Target?.EntityId ?? npc.Goals
            .FirstOrDefault(goal => goal.Code.Equals("travel", StringComparison.OrdinalIgnoreCase))?.TargetId;
        if (targetId is { } id)
        {
            var city = await dbContext.Cities.AsNoTracking().SingleOrDefaultAsync(value =>
                value.WorldId == npc.WorldId && value.Id == id && value.Status != CityStatus.Destroyed, cancellationToken);
            if (city is not null) return new(new Position(npc.WorldId, city.CenterX, city.CenterY), NpcActionTargetKind.WorldEntity, id);
            var actor = await dbContext.Actors.AsNoTracking().SingleOrDefaultAsync(value =>
                value.WorldId == npc.WorldId && value.Id == id && value.Status != ActorStatus.Dead, cancellationToken);
            if (actor is not null) return new(actor.Position, NpcActionTargetKind.Actor, id);
            return null;
        }
        return npc.Home is { } home ? new(home) : null;
    }
}
