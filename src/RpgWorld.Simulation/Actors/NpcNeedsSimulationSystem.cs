using RpgWorld.Application.Actors;
using RpgWorld.Domain.Actors;
using RpgWorld.Simulation.Engine;

namespace RpgWorld.Simulation.Actors;

public sealed class NpcNeedsSimulationSystem(INpcNeedsRepository repository) : ISimulationSystem
{
    public string Name => "NpcNeeds";
    public int Order => 20;
    public TimeSpan Frequency => SimulationSystemFrequencies.Economy;

    public async Task ExecuteAsync(
        SimulationTickContext context,
        CancellationToken cancellationToken = default)
    {
        var npcs = await repository.ListForUpdateAsync(context.WorldId, cancellationToken);
        var changed = false;
        foreach (var npc in npcs)
        {
            if (context.Clock.CurrentInstant <= npc.NeedsUpdatedAt) continue;
            npc.AdvanceNeedsTo(context.Clock.CurrentInstant);
            changed = true;
        }
        if (changed) await repository.SaveChangesAsync(cancellationToken);
    }
}
