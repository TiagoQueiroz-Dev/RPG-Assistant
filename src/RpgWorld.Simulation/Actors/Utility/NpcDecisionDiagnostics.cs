using System.Collections.Concurrent;

namespace RpgWorld.Simulation.Actors.Utility;

public sealed record NpcDecisionDiagnostic(
    Guid WorldId,
    Guid ActorId,
    DateTimeOffset DecidedAt,
    NpcDecision? Decision,
    string Explanation);

public interface INpcDecisionDiagnostics
{
    void Record(NpcDecisionDiagnostic diagnostic);
    NpcDecisionDiagnostic? GetLatest(Guid actorId);
    IReadOnlyList<NpcDecisionDiagnostic> ListLatest(Guid worldId);
}

public sealed class NpcDecisionDiagnostics : INpcDecisionDiagnostics
{
    private readonly ConcurrentDictionary<Guid, NpcDecisionDiagnostic> _latest = [];

    public void Record(NpcDecisionDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        _latest[diagnostic.ActorId] = diagnostic;
    }

    public NpcDecisionDiagnostic? GetLatest(Guid actorId) =>
        _latest.GetValueOrDefault(actorId);

    public IReadOnlyList<NpcDecisionDiagnostic> ListLatest(Guid worldId) =>
        _latest.Values
            .Where(diagnostic => diagnostic.WorldId == worldId)
            .OrderBy(diagnostic => diagnostic.ActorId)
            .ToArray();
}
