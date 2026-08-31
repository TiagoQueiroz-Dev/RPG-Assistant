using RpgWorld.Application.Worlds.Resources;
using RpgWorld.Simulation.Engine;

namespace RpgWorld.Simulation.Worlds.Resources;

public sealed class NaturalResourceRegenerationSystem(INaturalResourceRepository repository) : ISimulationSystem
{
    public string Name => "NaturalResourceRegeneration";
    public int Order => 35;
    public TimeSpan Frequency => SimulationSystemFrequencies.Economy;

    public async Task ExecuteAsync(SimulationTickContext context, CancellationToken cancellationToken = default)
    {
        var deposits = await repository.ListRegeneratingAsync(
            context.WorldId, context.Clock.CurrentInstant, cancellationToken);
        var changed = false;
        foreach (var deposit in deposits)
            changed |= deposit.RegenerateTo(context.Clock.CurrentInstant) > 0m;
        if (changed) await repository.SaveChangesAsync(cancellationToken);
    }
}
