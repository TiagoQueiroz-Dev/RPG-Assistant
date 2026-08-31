using RpgWorld.Application.Actors.Memories;
using RpgWorld.Simulation.Engine;

namespace RpgWorld.Simulation.Actors.Utility;

public sealed class NpcMemoryRetentionSimulationSystem(INpcMemoryRepository repository) : ISimulationSystem
{
    public string Name => "NpcMemoryRetention";
    public int Order => 25;
    public TimeSpan Frequency => SimulationSystemFrequencies.Population;

    public async Task ExecuteAsync(SimulationTickContext context, CancellationToken cancellationToken = default) =>
        _ = await repository.DeleteExpiredAsync(context.Clock.CurrentInstant, cancellationToken);
}
