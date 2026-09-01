using RpgWorld.Application.Actors;
using RpgWorld.Application.Worlds;
using RpgWorld.Domain.Actors;
using RpgWorld.Simulation.Engine;

namespace RpgWorld.Simulation.Actors;

public sealed class NpcNeedsSimulationSystem(
    INpcNeedsRepository repository,
    ICampaignSimulationSettingsProvider? settingsProvider = null) : ISimulationSystem
{
    public string Name => "NpcNeeds";
    public int Order => 20;
    public TimeSpan Frequency => SimulationSystemFrequencies.Economy;

    public async Task ExecuteAsync(
        SimulationTickContext context,
        CancellationToken cancellationToken = default)
    {
        var npcs = await repository.ListForUpdateAsync(context.WorldId, cancellationToken);
        var density = settingsProvider is null
            ? 1m
            : (await settingsProvider.GetEffectiveAsync(context.WorldId, cancellationToken)).NPCDensity;
        var processingCount = Math.Min(npcs.Count,
            Math.Max(1, (int)decimal.Ceiling(npcs.Count * Math.Min(1m, density))));
        var changed = false;
        foreach (var npc in npcs.Take(processingCount))
        {
            if (context.Clock.CurrentInstant <= npc.NeedsUpdatedAt) continue;
            npc.AdvanceNeedsTo(context.Clock.CurrentInstant);
            changed = true;
        }
        if (changed) await repository.SaveChangesAsync(cancellationToken);
    }
}
