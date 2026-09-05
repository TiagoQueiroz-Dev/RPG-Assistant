using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Cities;
using RpgWorld.Domain.Worlds.Resources;

namespace RpgWorld.Application.Actors.Actions;

public sealed record NpcFoodSource(ResourceDeposit Deposit, Position Position);
public interface INpcDailyActivityStore
{
    Task<NpcFoodSource?> FindFoodAsync(NpcActor npc, CancellationToken cancellationToken = default);
    Task<City?> GetWorkCityAsync(NpcActor npc, CancellationToken cancellationToken = default);
    Task<bool> CanRestAsync(NpcActor npc, Position position, CancellationToken cancellationToken = default);
}
