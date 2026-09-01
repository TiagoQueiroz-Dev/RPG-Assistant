using RpgWorld.Application.Worlds.Resources;
using RpgWorld.Application.Worlds;
using RpgWorld.Simulation.Engine;

namespace RpgWorld.Simulation.Worlds.Resources;

public sealed class NaturalResourceRegenerationSystem(
    INaturalResourceRepository repository,
    ICampaignSimulationSettingsProvider? settingsProvider = null) : ISimulationSystem
{
    public string Name => "NaturalResourceRegeneration";
    public int Order => 35;
    public TimeSpan Frequency => SimulationSystemFrequencies.Economy;

    public async Task ExecuteAsync(SimulationTickContext context, CancellationToken cancellationToken = default)
    {
        var deposits = await repository.ListRegeneratingAsync(
            context.WorldId, context.Clock.CurrentInstant, cancellationToken);
        var scarcity = settingsProvider is null
            ? 1m
            : (await settingsProvider.GetEffectiveAsync(context.WorldId, cancellationToken)).ResourceScarcity;
        var availability = 1m / scarcity;
        var changed = false;
        foreach (var deposit in deposits)
            changed |= deposit.RegenerateTo(context.Clock.CurrentInstant, availability) > 0m;
        if (changed) await repository.SaveChangesAsync(cancellationToken);
    }
}
