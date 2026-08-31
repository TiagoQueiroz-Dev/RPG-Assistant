using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RpgWorld.Simulation.Chunks;
using RpgWorld.Simulation.Time;
using RpgWorld.Simulation.Engine;
using RpgWorld.Simulation.Regions;
using RpgWorld.Simulation.Actors;
using RpgWorld.Application.Actors.Movement;
using RpgWorld.Simulation.Actors.Utility;
using RpgWorld.Domain.Actors.Traits;

namespace RpgWorld.Simulation;

public static class DependencyInjection
{
    public static IServiceCollection AddSimulation(
        this IServiceCollection services,
        ChunkActivationOptions? chunkActivationOptions = null,
        SimulationEngineOptions? simulationEngineOptions = null,
        SimulationLevelOptions? simulationLevelOptions = null,
        UtilityAiOptions? utilityAiOptions = null)
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
        var effectiveUtilityAiOptions = utilityAiOptions ?? new UtilityAiOptions();
        effectiveUtilityAiOptions.Validate();
        services.AddSingleton(effectiveUtilityAiOptions);
        services.AddSingleton<NpcAction, EatNpcAction>();
        services.AddSingleton<NpcAction, SleepNpcAction>();
        services.AddSingleton<NpcAction, WorkNpcAction>();
        services.AddSingleton<NpcAction, TravelNpcAction>();
        services.AddSingleton<NpcAction, AttackEnemyNpcAction>();
        services.AddSingleton<INpcDecisionContextProvider, DefaultNpcDecisionContextProvider>();
        services.TryAddSingleton<ITraitDefinitionCatalog>(new TraitDefinitionCatalog([]));
        services.AddSingleton<INpcUtilityScoreModifier, TraitUtilityScoreModifier>();
        services.AddSingleton<INpcUtilityDecisionService, NpcUtilityDecisionService>();
        services.AddSingleton<INpcDecisionDiagnostics, NpcDecisionDiagnostics>();
        services.AddScoped<ISimulationSystem, NpcUtilityAiSimulationSystem>();
        services.AddHostedService<SimulationEngine>();
        return services;
    }
}
