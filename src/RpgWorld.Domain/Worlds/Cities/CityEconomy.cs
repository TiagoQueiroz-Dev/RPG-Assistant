using RpgWorld.Domain.Worlds.Definitions;

namespace RpgWorld.Domain.Worlds.Cities;

public enum CityMarketCondition { Balanced, Shortage, Surplus }

public sealed record CityResourceEconomyRule
{
    public CityResourceEconomyRule(
        string resourceCode,
        decimal consumptionPerResident,
        decimal basePrice,
        decimal targetStockPerResident,
        decimal criticalStockRatio = 0.25m,
        decimal surplusStockRatio = 2m,
        decimal maximumPriceMultiplier = 4m,
        decimal minimumPriceMultiplier = 0.5m)
    {
        ResourceCode = DefinitionCode.Normalize(resourceCode, nameof(resourceCode));
        if (consumptionPerResident < 0m) throw new ArgumentOutOfRangeException(nameof(consumptionPerResident));
        if (basePrice <= 0m) throw new ArgumentOutOfRangeException(nameof(basePrice));
        if (targetStockPerResident <= 0m) throw new ArgumentOutOfRangeException(nameof(targetStockPerResident));
        if (criticalStockRatio is < 0m or >= 1m) throw new ArgumentOutOfRangeException(nameof(criticalStockRatio));
        if (surplusStockRatio <= 1m) throw new ArgumentOutOfRangeException(nameof(surplusStockRatio));
        if (maximumPriceMultiplier < 1m) throw new ArgumentOutOfRangeException(nameof(maximumPriceMultiplier));
        if (minimumPriceMultiplier is <= 0m or > 1m) throw new ArgumentOutOfRangeException(nameof(minimumPriceMultiplier));
        ConsumptionPerResident = consumptionPerResident;
        BasePrice = basePrice;
        TargetStockPerResident = targetStockPerResident;
        CriticalStockRatio = criticalStockRatio;
        SurplusStockRatio = surplusStockRatio;
        MaximumPriceMultiplier = maximumPriceMultiplier;
        MinimumPriceMultiplier = minimumPriceMultiplier;
    }

    public string ResourceCode { get; }
    public decimal ConsumptionPerResident { get; }
    public decimal BasePrice { get; }
    public decimal TargetStockPerResident { get; }
    public decimal CriticalStockRatio { get; }
    public decimal SurplusStockRatio { get; }
    public decimal MaximumPriceMultiplier { get; }
    public decimal MinimumPriceMultiplier { get; }
}

public sealed record CityResourceMarketSnapshot(
    string ResourceCode,
    decimal OpeningStock,
    decimal Produced,
    decimal Demand,
    decimal Consumed,
    decimal UnmetDemand,
    decimal ClosingStock,
    decimal UnitPrice,
    CityMarketCondition Condition,
    DateTimeOffset UpdatedAtUtc);

public sealed record CityEconomicCycleResult(
    Guid CityId,
    long CycleNumber,
    DateTimeOffset OccurredAtUtc,
    IReadOnlyList<CityResourceMarketSnapshot> Markets);
