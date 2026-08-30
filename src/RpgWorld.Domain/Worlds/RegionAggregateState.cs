namespace RpgWorld.Domain.Worlds;

public sealed record RegionAggregateState
{
    public RegionAggregateState(
        int population,
        decimal economicOutput,
        decimal militaryStrength,
        decimal productionOutput)
    {
        if (population < 0) throw new ArgumentOutOfRangeException(nameof(population));
        if (economicOutput < 0) throw new ArgumentOutOfRangeException(nameof(economicOutput));
        if (militaryStrength < 0) throw new ArgumentOutOfRangeException(nameof(militaryStrength));
        if (productionOutput < 0) throw new ArgumentOutOfRangeException(nameof(productionOutput));
        Population = population;
        EconomicOutput = economicOutput;
        MilitaryStrength = militaryStrength;
        ProductionOutput = productionOutput;
    }

    public int Population { get; }
    public decimal EconomicOutput { get; }
    public decimal MilitaryStrength { get; }
    public decimal ProductionOutput { get; }

    public static RegionAggregateState Empty { get; } = new(0, 0, 0, 0);
}
