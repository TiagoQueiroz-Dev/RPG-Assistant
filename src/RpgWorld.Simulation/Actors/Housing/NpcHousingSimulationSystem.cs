using RpgWorld.Application.Actors.Housing;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Actors.Housing;
using RpgWorld.Simulation.Engine;

namespace RpgWorld.Simulation.Actors.Housing;

public sealed class NpcHousingSimulationSystem(
    INpcHousingRepository repository,
    NpcHousingOptions options) : ISimulationSystem
{
    public string Name => "NpcHousing";
    public int Order => 40;
    public TimeSpan Frequency => SimulationSystemFrequencies.Population;

    public async Task ExecuteAsync(SimulationTickContext context, CancellationToken cancellationToken = default)
    {
        options.Validate();
        var world = await repository.GetWorldAsync(context.WorldId, cancellationToken);
        if (world is null) return;
        var changed = false;
        var active = await repository.ListInProgressAsync(context.WorldId, cancellationToken);
        foreach (var construction in active)
        {
            if (await repository.GetNpcAsync(construction.OwnerActorId, cancellationToken) is not { } owner ||
                owner.Status == ActorStatus.Dead ||
                !construction.CanAdvance(owner)) continue;
            construction.Advance(owner, context.Clock.CurrentInstant);
            if (construction.Status == HousingConstructionStatus.Completed)
            {
                foreach (var residentId in construction.ResidentActorIds)
                {
                    var resident = residentId == owner.Id
                        ? owner
                        : await repository.GetNpcAsync(residentId, cancellationToken);
                    if (resident is null || resident.Status == ActorStatus.Dead || resident.WorldId != world.Id ||
                        (resident.Id != owner.Id && resident.Home is not null)) continue;
                    resident.SetHome(world, construction.Position, context.Clock.CurrentInstant, construction.Id);
                    if (resident.Goals.Any(goal => goal.Code == NpcGoalCodes.NeedHouse))
                        resident.RemoveGoal(NpcGoalCodes.NeedHouse, context.Clock.CurrentInstant);
                }
            }
            changed = true;
        }

        // Persist completed stages before querying homeless NPCs, and make each
        // reservation visible to the next NPC evaluated in this same cycle.
        if (changed)
        {
            await repository.SaveChangesAsync(cancellationToken);
            changed = false;
        }

        var activeOwners = active.Select(construction => construction.OwnerActorId).ToHashSet();
        var homeless = await repository.ListHomelessAsync(context.WorldId, cancellationToken);
        foreach (var npc in homeless.Where(npc => !activeOwners.Contains(npc.Id)))
        {
            if (!npc.Goals.Any(goal => goal.Code == NpcGoalCodes.NeedHouse))
            {
                npc.SetGoal(NpcGoalCodes.NeedHouse, 70, null, context.Clock.CurrentInstant);
                changed = true;
            }
            if (npc.InventoryQuantity("wood") < options.RequiredWood ||
                npc.InventoryQuantity("stone") < options.RequiredStone) continue;
            var tile = await repository.FindBuildableTileAsync(
                context.WorldId, npc.X, npc.Y, options.SearchRadius, options.AllowedTerrainCodes, cancellationToken);
            if (tile is null) continue;
            var construction = HousingConstruction.Create(
                npc, tile.Position, options.RequiredWood, options.RequiredStone, context.Clock.CurrentInstant);
            construction.Advance(npc, context.Clock.CurrentInstant);
            tile.AssignStructure(construction.Id);
            repository.Add(construction);
            activeOwners.Add(npc.Id);
            await repository.SaveChangesAsync(cancellationToken);
            changed = false;
        }
        if (changed) await repository.SaveChangesAsync(cancellationToken);
    }
}
