using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RpgWorld.Simulation.Engine;

public sealed class SimulationSystemRunner(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ISimulationScheduler scheduler,
    ILogger<SimulationSystemRunner> logger) : ISimulationSystemRunner
{
    public async Task RunAsync(SimulationTickContext context, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var systems = scope.ServiceProvider.GetServices<ISimulationSystem>()
            .OrderBy(system => system.Order)
            .ThenBy(system => system.Name, StringComparer.Ordinal)
            .ToArray();
        foreach (var system in systems)
        {
            var observedAt = timeProvider.GetUtcNow();
            if (!scheduler.TryBegin(context.WorldId, system, observedAt, out var execution)) continue;
            var started = timeProvider.GetTimestamp();
            var succeeded = false;
            try
            {
                await system.ExecuteAsync(context, cancellationToken);
                succeeded = true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "Simulation system {SystemName} failed for world {WorldId}; remaining systems will continue.",
                    system.Name, context.WorldId);
            }
            finally
            {
                var duration = timeProvider.GetElapsedTime(started);
                scheduler.Complete(execution!, execution!.StartedAtUtc.Add(duration), duration, succeeded);
            }
        }
    }
}
