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
        var systems = scope.ServiceProvider
            .GetServices<ISimulationSystem>()
            .OrderBy(system => system.Order)
            .ThenBy(system => system.Name, StringComparer.Ordinal)
            .ToArray();
        var worldIds = await repository.ListRunningWorldIdsAsync(cancellationToken);

        foreach (var worldId in worldIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var clock = await clockService.AdvanceTicksAsync(
                    worldId,
                    cancellationToken: cancellationToken);
                var context = new SimulationTickContext(worldId, clock);

                foreach (var system in systems)
                {
                    try
                    {
                        await system.ExecuteAsync(context, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(
                            exception,
                            "Simulation system {SystemName} failed for world {WorldId}; remaining systems will continue.",
                            system.Name,
                            worldId);
                    }
                }
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
