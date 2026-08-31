namespace RpgWorld.Simulation.Worlds.Factions;

public sealed class WarDeclarationOptions
{
    public decimal DeclareWarThreshold { get; init; } = 65m;
    public decimal BorderConflictWeight { get; init; } = 25m;
    public decimal ResourceDisputeWeight { get; init; } = 20m;
    public decimal HistoricalHatredWeight { get; init; } = 25m;
    public decimal AggressiveLeaderWeight { get; init; } = 15m;
    public decimal WeakEnemyWeight { get; init; } = 15m;

    public void Validate()
    {
        if (DeclareWarThreshold is < 0m or > 100m)
            throw new ArgumentOutOfRangeException(nameof(DeclareWarThreshold));
        var weights = new[] { BorderConflictWeight, ResourceDisputeWeight, HistoricalHatredWeight,
            AggressiveLeaderWeight, WeakEnemyWeight };
        if (weights.Any(weight => weight < 0m) || weights.Sum() <= 0m)
            throw new ArgumentException("War score weights must be non-negative and have a positive sum.");
    }
}
