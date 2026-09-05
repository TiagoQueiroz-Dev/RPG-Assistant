using System.Collections.Concurrent;

namespace RpgWorld.Simulation.Actors.Actions;

public sealed record NpcActionExecutionDiagnostic(Guid WorldId, Guid ActorId, Guid ExecutionId,
    string ActionCode, DateTimeOffset Instant, NpcActionStepOutcome Outcome, string? Reason);

public sealed class NpcActionExecutionDiagnostics
{
    private readonly ConcurrentDictionary<Guid, NpcActionExecutionDiagnostic> _latest = [];
    public void Record(NpcActionExecutionDiagnostic diagnostic) => _latest[diagnostic.ActorId] = diagnostic;
    public NpcActionExecutionDiagnostic? GetLatest(Guid actorId) => _latest.GetValueOrDefault(actorId);
}
