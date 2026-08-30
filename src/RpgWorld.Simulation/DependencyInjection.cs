using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RpgWorld.Simulation.Chunks;
using RpgWorld.Simulation.Time;
using RpgWorld.Simulation.Engine;

namespace RpgWorld.Simulation;

public static class DependencyInjection
{
    public static IServiceCollection AddSimulation(
        this IServiceCollection services,
        ChunkActivationOptions? chunkActivationOptions = null,
        SimulationEngineOptions? simulationEngineOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(chunkActivationOptions ?? new ChunkActivationOptions());
        services.AddSingleton(simulationEngineOptions ?? new SimulationEngineOptions());
        services.AddSingleton<ISimulationScheduler, SimulationScheduler>();
        services.AddScoped<IChunkActivationService, ChunkActivationService>();
        services.AddScoped<IWorldClockService, WorldClockService>();
        services.AddScoped<IWorldSimulationControlService, WorldSimulationControlService>();
        services.AddHostedService<SimulationEngine>();
        return services;
    }
}
