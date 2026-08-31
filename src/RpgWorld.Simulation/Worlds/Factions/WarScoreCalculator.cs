using RpgWorld.Application.Worlds.Factions;
using RpgWorld.Domain.Worlds.Factions;

namespace RpgWorld.Simulation.Worlds.Factions;

public sealed class WarScoreCalculator(WarDeclarationOptions options)
{
    public FactionWarScore Calculate(
        Faction source,
        Faction target,
        FactionWarContext context,
        DateTimeOffset evaluatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(context);
        options.Validate();
        if (source.WorldId != target.WorldId || source.Id == target.Id)
            throw new InvalidOperationException("War score requires distinct factions in the same world.");

        var relation = source.Relations.GetValueOrDefault(target.Id);
        var borderHistory = relation?.History
            .Where(change => change.Source == FactionRelationModifierSource.Border)
            .Sum(change => Math.Max(0, change.TensionDelta)) ?? 0;
        var historicalChanges = relation?.History
            .Where(change => change.Source == FactionRelationModifierSource.History)
            .Sum(change => Math.Max(0, change.TensionDelta) + Math.Max(0, -change.AffinityDelta)) ?? 0;
        var border = Clamp((context.SharedBorderEdges * 20m) + borderHistory);
        var resources = Clamp((context.SourceCriticalShortageMarkets * 25m) +
            (context.SourceCriticalShortageMarkets > 0 && context.TargetStoredResources > 0m ? 25m : 0m));
        var hatred = Clamp(Math.Max(0, -(relation?.Affinity ?? 0)) +
            (relation?.Tension ?? 0) + historicalChanges);
        var aggressive = context.AggressiveLeader ? 100m : 0m;
        var weakEnemy = source.MilitaryPower <= 0m || source.MilitaryPower <= target.MilitaryPower
            ? 0m
            : Clamp((1m - (target.MilitaryPower / source.MilitaryPower)) * 100m);
        var factors = new FactionWarFactors(border, resources, hatred, aggressive, weakEnemy);
        var weightTotal = options.BorderConflictWeight + options.ResourceDisputeWeight +
            options.HistoricalHatredWeight + options.AggressiveLeaderWeight + options.WeakEnemyWeight;
        var total = Math.Round(
            ((border * options.BorderConflictWeight) + (resources * options.ResourceDisputeWeight) +
             (hatred * options.HistoricalHatredWeight) + (aggressive * options.AggressiveLeaderWeight) +
             (weakEnemy * options.WeakEnemyWeight)) / weightTotal,
            2, MidpointRounding.AwayFromZero);
        return new FactionWarScore(factors, total, options.DeclareWarThreshold, evaluatedAtUtc.ToUniversalTime());
    }

    private static decimal Clamp(decimal value) => Math.Clamp(value, 0m, 100m);
}
