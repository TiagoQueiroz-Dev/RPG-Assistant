using Microsoft.Extensions.Logging;
using RpgWorld.Application.Actors.Actions;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Actors.Actions;
using RpgWorld.Simulation.Engine;

namespace RpgWorld.Simulation.Actors.Actions;

public sealed class NpcActionExecutionSimulationSystem(
    INpcActionExecutionStore store,
    IEnumerable<INpcActionExecutor> executors,
    NpcActionExecutionDiagnostics diagnostics,
    ILogger<NpcActionExecutionSimulationSystem> logger) : ISimulationSystem
{
    private readonly IReadOnlyDictionary<string, INpcActionExecutor> _executors =
        executors.ToDictionary(executor => executor.ActionCode, StringComparer.Ordinal);

    public string Name => "NpcActionExecution";
    public int Order => 35;
    public TimeSpan Frequency => SimulationSystemFrequencies.Movement;

    public async Task ExecuteAsync(SimulationTickContext context, CancellationToken cancellationToken = default)
    {
        var ids = await store.ListCandidatesAsync(context.WorldId, cancellationToken);
        foreach (var actorId in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            NpcActionExecution? execution = null;
            NpcActionStepResult? result = null;
            try
            {
                await store.ExecuteAtomicallyAsync(async token =>
                {
                    var npc = await store.GetAsync(context.WorldId, actorId, token);
                    if (npc is null || npc.Status == ActorStatus.Dead || npc.ActionExecution is not { } current ||
                        !current.CanProcess(context.Clock.CurrentInstant)) return;
                    execution = current;
                    context.RecordActorsProcessed(1);
                    result = _executors.TryGetValue(current.ActionCode, out var executor)
                        ? await executor.ExecuteAsync(new(npc, current, context.Clock.CurrentInstant), token)
                        : new(NpcActionStepOutcome.Fail, Reason: $"No executor registered for '{current.ActionCode}'.");
                    if (!Enum.IsDefined(result.Outcome)) throw new InvalidOperationException("Executor returned an invalid outcome.");
                    if (result.Outcome is NpcActionStepOutcome.Fail or NpcActionStepOutcome.Cancel)
                        throw new RejectedStepException(result);
                    npc.AdvanceAction(current.Id, result.Outcome == NpcActionStepOutcome.Complete ? 1m : result.Progress,
                        context.Clock.CurrentInstant);
                    if (result.Outcome == NpcActionStepOutcome.Complete)
                        npc.FinishAction(current.Id, NpcActionStatus.Completed, context.Clock.CurrentInstant);
                    await store.SaveChangesAsync(token);
                }, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                result = exception is RejectedStepException rejected ? rejected.Result
                    : new(NpcActionStepOutcome.Fail, Reason: $"{exception.GetType().Name}: {exception.Message}");
                logger.LogWarning(exception, "Action execution failed for NPC {ActorId}; remaining NPCs will continue.", actorId);
                if (execution is not null)
                {
                    try
                    {
                        await store.ExecuteAtomicallyAsync(async token =>
                        {
                            var npc = await store.GetAsync(context.WorldId, actorId, token);
                            if (npc is { Status: not ActorStatus.Dead, ActionExecution: { Status: NpcActionStatus.Running } current } &&
                                current.Id == execution.Id)
                            {
                                var reason = string.IsNullOrWhiteSpace(result.Reason) ? "Action could not continue." : result.Reason;
                                npc.FinishAction(current.Id, result.Outcome == NpcActionStepOutcome.Cancel
                                    ? NpcActionStatus.Cancelled : NpcActionStatus.Failed,
                                    context.Clock.CurrentInstant, reason[..Math.Min(reason.Length, 500)]);
                                await store.SaveChangesAsync(token);
                            }
                        }, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch (Exception persistenceError)
                    {
                        logger.LogError(persistenceError, "Could not persist failure for NPC {ActorId}.", actorId);
                    }
                }
            }
            if (execution is not null && result is not null)
            {
                diagnostics.Record(new(context.WorldId, actorId, execution.Id, execution.ActionCode,
                    context.Clock.CurrentInstant, result.Outcome, result.Reason));
                logger.LogDebug("NPC {ActorId} action {ActionCode} execution {ExecutionId}: {Outcome} ({Reason}).",
                    actorId, execution.ActionCode, execution.Id, result.Outcome, result.Reason);
            }
        }
    }

    private sealed class RejectedStepException(NpcActionStepResult result) : Exception(result.Reason)
    {
        public NpcActionStepResult Result { get; } = result;
    }
}
