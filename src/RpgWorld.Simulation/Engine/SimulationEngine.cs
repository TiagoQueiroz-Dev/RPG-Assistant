using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RpgWorld.Application.Worlds;
using RpgWorld.Simulation.Time;

namespace RpgWorld.Simulation.Engine;

public sealed class SimulationEngine(
    IServiceScopeFactory scopeFactory,
    SimulationEngineOptions options,
    TimeProvider timeProvider,
    IWorldCommandGate commandGate,
    ISimulationSystemRunner systemRunner,
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
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider
            .GetRequiredService<IWorldSimulationRepository>();
        var clockService = scope.ServiceProvider
            .GetRequiredService<IWorldClockService>();
        var worldIds = await repository.ListRunningWorldIdsAsync(cancellationToken);

        foreach (var worldId in worldIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await commandGate.ExecuteAsync(worldId, async token =>
                {
                    var currentWorld = await repository.GetAsync(worldId, token);
                    if (currentWorld?.IsSimulationRunning != true) return;
                    var clock = await clockService.SynchronizeAsync(worldId, token);
                    await systemRunner.RunAsync(new SimulationTickContext(worldId, clock), token);
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
}
