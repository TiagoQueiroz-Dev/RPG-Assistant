using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RpgWorld.Application.Worlds;
using RpgWorld.Simulation.Chunks;
using RpgWorld.Simulation.Time;

namespace RpgWorld.Simulation.Engine;

public sealed class SimulationEngine(
    IServiceScopeFactory scopeFactory,
    SimulationEngineOptions options,
    TimeProvider timeProvider,
    IWorldCommandGate commandGate,
    ISimulationSystemRunner systemRunner,
    ActiveChunkRegistry activeChunks,
    ISimulationPerformanceMetrics metrics,
    ILogger<SimulationEngine> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        options.Validate();
        logger.LogInformation(
            "Simulation engine started with a {TickInterval} tick interval.",
            options.TickInterval);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunCycleAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Simulation engine cycle failed; processing will continue.");
                }

                await Task.Delay(options.TickInterval, timeProvider, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
        finally
        {
            logger.LogInformation("Simulation engine stopped gracefully.");
        }
    }

    public async Task RunCycleAsync(CancellationToken cancellationToken = default)
    {
        var cycleStarted = timeProvider.GetTimestamp();
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider
            .GetRequiredService<IWorldSimulationRepository>();
        var clockService = scope.ServiceProvider
            .GetRequiredService<IWorldClockService>();
        var worldIds = await repository.ListRunningWorldIdsAsync(cancellationToken);

        try
        {
            foreach (var worldId in worldIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await commandGate.ExecuteAsync(worldId, async token =>
                    {
                        var currentWorld = await repository.GetAsync(worldId, token);
                        if (currentWorld?.IsSimulationRunning != true) return;
                        var started = timeProvider.GetTimestamp();
                        var clock = await clockService.SynchronizeAsync(worldId, token);
                        var workload = new SimulationTickWorkload(activeChunks.Count(worldId));
                        await systemRunner.RunAsync(new SimulationTickContext(worldId, clock, workload), token);
                        var duration = timeProvider.GetElapsedTime(started);
                        metrics.RecordTick(worldId, duration, workload, options.TickBudget);
                        logger.LogDebug(
                            "Simulation tick completed for world {WorldId} in {DurationMs} ms; " +
                            "processed {ActorsProcessed} actors across {ActiveChunks} active chunks.",
                            worldId, duration.TotalMilliseconds, workload.ActorsProcessed, workload.ActiveChunks);
                        if (duration > options.TickBudget)
                            logger.LogWarning(
                                "Simulation tick exceeded its processing budget for world {WorldId}: " +
                                "{DurationMs} ms > {BudgetMs} ms; actors: {ActorsProcessed}; active chunks: {ActiveChunks}.",
                                worldId, duration.TotalMilliseconds, options.TickBudget.TotalMilliseconds,
                                workload.ActorsProcessed, workload.ActiveChunks);
                    }, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Simulation tick failed for world {WorldId}; remaining worlds will continue.",
                        worldId);
                }
            }
        }
        finally
        {
            metrics.RecordCycle(worldIds.Count, timeProvider.GetElapsedTime(cycleStarted));
        }
    }
}
