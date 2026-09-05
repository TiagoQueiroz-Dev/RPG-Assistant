using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Actors.Actions;

namespace RpgWorld.Simulation.Actors.Actions;

public enum NpcActionStepOutcome { Continue, Complete, Fail, Cancel }
public sealed record NpcActionStepResult(NpcActionStepOutcome Outcome, decimal Progress = 0m, string? Reason = null);
public sealed record NpcActionExecutionContext(NpcActor Actor, NpcActionExecution Execution, DateTimeOffset Instant)
{
    public TimeSpan Elapsed => Instant - (Execution.LastProcessedAt ?? Execution.StartedAt);
}

public interface INpcActionExecutor
{
    string ActionCode { get; }
    Task<NpcActionStepResult> ExecuteAsync(NpcActionExecutionContext context, CancellationToken cancellationToken = default);
}
