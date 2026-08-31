using RpgWorld.Application.Worlds.Factions;
using RpgWorld.Domain.Worlds.Factions;
using RpgWorld.Simulation.Engine;

namespace RpgWorld.Simulation.Worlds.Factions;

public sealed class FactionWarDeclarationSimulationSystem(
    IFactionWarRepository repository,
    WarScoreCalculator calculator) : ISimulationSystem
{
    public string Name => "Diplomacy";
    public int Order => 60;
    public TimeSpan Frequency => SimulationSystemFrequencies.Diplomacy;

    public async Task ExecuteAsync(SimulationTickContext context, CancellationToken cancellationToken = default)
    {
        var factions = await repository.ListActiveAsync(context.WorldId, cancellationToken);
        if (factions.Count < 2) return;
        foreach (var source in factions.OrderBy(faction => faction.Id))
        foreach (var target in factions.OrderBy(faction => faction.Id))
        {
            if (source.Id == target.Id ||
                source.Relations.GetValueOrDefault(target.Id)?.Kind == FactionRelationKind.War) continue;
            var warContext = await repository.BuildContextAsync(source, target, cancellationToken);
            var score = calculator.Calculate(source, target, warContext, context.Clock.CurrentInstant);
            source.RecordWarAssessment(target.Id, score, context.Clock.CurrentInstant);
            if (score.ReachedThreshold &&
                !(source.Relations.GetValueOrDefault(target.Id)?.IsWarPreventedAt(context.Clock.CurrentInstant) ?? false))
                source.DeclareWar(target.Id, score, "Emergent diplomatic tensions exceeded the war threshold.", false,
                    context.Clock.CurrentInstant);
        }
        await repository.SaveChangesAsync(cancellationToken);
    }
}
