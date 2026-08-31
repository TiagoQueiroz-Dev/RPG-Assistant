using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RpgWorld.Simulation.Chunks;
using RpgWorld.Simulation.Time;
using RpgWorld.Simulation.Engine;
using RpgWorld.Simulation.Regions;
using RpgWorld.Simulation.Actors;
using RpgWorld.Application.Actors.Movement;

namespace RpgWorld.Simulation;

public static class DependencyInjection
{
    public static IServiceCollection AddSimulation(
        this IServiceCollection services,
        ChunkActivationOptions? chunkActivationOptions = null,
        SimulationEngineOptions? simulationEngineOptions = null,
        SimulationLevelOptions? simulationLevelOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(chunkActivationOptions ?? new ChunkActivationOptions());
        services.AddSingleton<ActiveChunkRegistry>();
        services.AddSingleton(simulationEngineOptions ?? new SimulationEngineOptions());
        services.AddSingleton<ISimulationScheduler, SimulationScheduler>();
        services.AddSingleton<IWorldCommandGate, WorldCommandGate>();
        services.AddSingleton<ISimulationSystemRunner, SimulationSystemRunner>();
        services.AddSingleton(simulationLevelOptions ?? new SimulationLevelOptions());
        services.AddSingleton<SimulationLevelResolver>();
        services.AddScoped<IChunkActivationService, ChunkActivationService>();
        services.AddScoped<IWorldClockService, WorldClockService>();
        services.AddScoped<IWorldTimeCommandService, WorldTimeCommandService>();
        services.AddScoped<IWorldSimulationControlService, WorldSimulationControlService>();
        services.AddScoped<IRegionSimulationService, RegionSimulationService>();
        services.AddSingleton<IActorMovementPolicy, AdjacentTileMovementPolicy>();
        services.AddScoped<IActorMovementService, ActorMovementService>();
        services.AddScoped<ISimulationSystem, NpcNeedsSimulationSystem>();
        services.AddHostedService<SimulationEngine>();
        return services;
    }
}
