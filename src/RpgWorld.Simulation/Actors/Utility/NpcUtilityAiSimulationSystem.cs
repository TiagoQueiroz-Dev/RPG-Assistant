using Microsoft.Extensions.Logging;
using RpgWorld.Application.Actors;
using RpgWorld.Simulation.Engine;

namespace RpgWorld.Simulation.Actors.Utility;

public sealed class NpcUtilityAiSimulationSystem(
    INpcNeedsRepository repository,
    INpcDecisionContextProvider contextProvider,
    INpcUtilityDecisionService decisionService,
    INpcDecisionDiagnostics diagnostics,
    ILogger<NpcUtilityAiSimulationSystem> logger) : ISimulationSystem
{
    public string Name => "NpcUtilityAi";
    public int Order => 30;
    public TimeSpan Frequency => SimulationSystemFrequencies.NpcDecisions;

    public async Task ExecuteAsync(
        SimulationTickContext context,
        CancellationToken cancellationToken = default)
    {
        var npcs = await repository.ListForUpdateAsync(context.WorldId, cancellationToken);
        var changed = false;
        foreach (var npc in npcs)
        {
            var decision = decisionService.Decide(contextProvider.Create(npc));
            var explanation = decision?.Explain() ?? "No eligible NPC action was available.";
            diagnostics.Record(new NpcDecisionDiagnostic(
                context.WorldId,
                npc.Id,
                context.Clock.CurrentInstant,
                decision,
                explanation));
            logger.LogDebug(
                "NPC utility decision for {ActorId} in world {WorldId}: {Explanation}",
                npc.Id,
                context.WorldId,
                explanation);
            var actionCode = decision?.ActionCode;
            if (string.Equals(npc.CurrentAction, actionCode, StringComparison.Ordinal)) continue;
            npc.SetCurrentAction(actionCode, context.Clock.CurrentInstant);
            changed = true;
        }
        if (changed) await repository.SaveChangesAsync(cancellationToken);
    }
}
