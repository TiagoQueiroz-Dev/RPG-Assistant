using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RpgWorld.Simulation.Engine;

public sealed class SimulationSystemRunner(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ISimulationScheduler scheduler,
    SimulationEngineOptions options,
    ISimulationPerformanceMetrics metrics,
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
            var actorsBefore = context.Workload?.ActorsProcessed ?? 0;
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
                var actorsProcessed = (context.Workload?.ActorsProcessed ?? 0) - actorsBefore;
                var budget = options.GetSystemBudget(system.Name);
                scheduler.Complete(execution!, execution!.StartedAtUtc.Add(duration), duration, succeeded);
                metrics.RecordSystem(context.WorldId, system.Name, duration, actorsProcessed, budget, succeeded);
                logger.LogDebug(
                    "Simulation system {SystemName} completed for world {WorldId} in {DurationMs} ms; " +
                    "processed {ActorsProcessed} actors with a {BudgetMs} ms budget.",
                    system.Name, context.WorldId, duration.TotalMilliseconds, actorsProcessed, budget.TotalMilliseconds);
                if (duration > budget)
                    logger.LogWarning(
                        "Simulation system {SystemName} exceeded its processing budget for world {WorldId}: " +
                        "{DurationMs} ms > {BudgetMs} ms; actors processed: {ActorsProcessed}.",
                        system.Name, context.WorldId, duration.TotalMilliseconds, budget.TotalMilliseconds,
                        actorsProcessed);
            }
        }
    }
}
