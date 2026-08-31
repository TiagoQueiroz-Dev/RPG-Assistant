namespace RpgWorld.Domain.Worlds.Factions;

public sealed record FactionWarFactors
{
    public FactionWarFactors(
        decimal borderConflict,
        decimal resourceDispute,
        decimal historicalHatred,
        decimal aggressiveLeader,
        decimal weakEnemy)
    {
        BorderConflict = Factor(borderConflict, nameof(borderConflict));
        ResourceDispute = Factor(resourceDispute, nameof(resourceDispute));
        HistoricalHatred = Factor(historicalHatred, nameof(historicalHatred));
        AggressiveLeader = Factor(aggressiveLeader, nameof(aggressiveLeader));
        WeakEnemy = Factor(weakEnemy, nameof(weakEnemy));
    }

    public decimal BorderConflict { get; init; }
    public decimal ResourceDispute { get; init; }
    public decimal HistoricalHatred { get; init; }
    public decimal AggressiveLeader { get; init; }
    public decimal WeakEnemy { get; init; }

    private static decimal Factor(decimal value, string parameterName) =>
        value is < 0m or > 100m ? throw new ArgumentOutOfRangeException(parameterName) : value;
}

public sealed record FactionWarScore
{
    public FactionWarScore(
        FactionWarFactors factors,
        decimal total,
        decimal declareWarThreshold,
        DateTimeOffset evaluatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(factors);
        if (total is < 0m or > 100m) throw new ArgumentOutOfRangeException(nameof(total));
        if (declareWarThreshold is < 0m or > 100m)
            throw new ArgumentOutOfRangeException(nameof(declareWarThreshold));
        Factors = factors;
        Total = total;
        DeclareWarThreshold = declareWarThreshold;
        EvaluatedAtUtc = evaluatedAtUtc.ToUniversalTime();
    }

    public FactionWarFactors Factors { get; init; }
    public decimal Total { get; init; }
    public decimal DeclareWarThreshold { get; init; }
    public DateTimeOffset EvaluatedAtUtc { get; init; }
    public bool ReachedThreshold => Total >= DeclareWarThreshold;
}
