using RpgWorld.Domain.Worlds.Cities;

namespace RpgWorld.Simulation.Worlds.Economy;

public sealed class CityEconomyOptions
{
    public List<CityEconomyResourceOptions> Resources { get; set; } = [];

    public static CityEconomyOptions CreateDefault() => new()
    {
        Resources =
        [
            new() { ResourceCode = "food", NaturalResourceCode = "food", NaturalExtractionPerResident = 1.10m,
                ConsumptionPerResident = 1m, ProductionPerBuilding = 2m, BasePrice = 2m, TargetStockPerResident = 2m },
            new() { ResourceCode = "wood", NaturalResourceCode = "wood", NaturalExtractionPerResident = 0.15m,
                ConsumptionPerResident = 0.10m, ProductionPerBuilding = 0.50m, BasePrice = 4m, TargetStockPerResident = 1m },
            new() { ResourceCode = "stone", NaturalResourceCode = "stone", NaturalExtractionPerResident = 0.08m,
                ConsumptionPerResident = 0.05m, ProductionPerBuilding = 0.25m, BasePrice = 6m, TargetStockPerResident = 0.50m },
            new() { ResourceCode = "gold", NaturalResourceCode = "gold", NaturalExtractionPerResident = 0.03m,
                ConsumptionPerResident = 0.02m, ProductionPerBuilding = 0.05m, BasePrice = 20m, TargetStockPerResident = 0.25m }
        ]
    };

    public void Validate()
    {
        if (Resources is not { Count: > 0 })
            throw new InvalidOperationException("At least one city economy resource is required.");
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var resource in Resources)
        {
            resource.Validate();
            if (!codes.Add(resource.NormalizedResourceCode))
                throw new InvalidOperationException($"Duplicate city economy resource '{resource.ResourceCode}'.");
        }
    }
}

public sealed class CityEconomyResourceOptions
{
    public string ResourceCode { get; set; } = string.Empty;
    public string? NaturalResourceCode { get; set; }
    public decimal BaselineProductionPerResident { get; set; }
    public decimal NaturalExtractionPerResident { get; set; }
    public decimal ProductionPerBuilding { get; set; }
    public decimal ConsumptionPerResident { get; set; }
    public decimal BasePrice { get; set; }
    public decimal TargetStockPerResident { get; set; }
    public decimal CriticalStockRatio { get; set; } = 0.25m;
    public decimal SurplusStockRatio { get; set; } = 2m;
    public decimal MaximumPriceMultiplier { get; set; } = 4m;
    public decimal MinimumPriceMultiplier { get; set; } = 0.5m;

    public string NormalizedResourceCode => Normalize(ResourceCode, nameof(ResourceCode));
    public string? NormalizedNaturalResourceCode => string.IsNullOrWhiteSpace(NaturalResourceCode)
        ? null
        : Normalize(NaturalResourceCode, nameof(NaturalResourceCode));

    public void Validate()
    {
        _ = ToRule();
        _ = NormalizedNaturalResourceCode;
        if (BaselineProductionPerResident < 0m) throw new InvalidOperationException("Baseline production cannot be negative.");
        if (NaturalExtractionPerResident < 0m) throw new InvalidOperationException("Natural extraction cannot be negative.");
        if (ProductionPerBuilding < 0m) throw new InvalidOperationException("Building production cannot be negative.");
        if (NaturalExtractionPerResident > 0m && NormalizedNaturalResourceCode is null)
            throw new InvalidOperationException($"Resource '{ResourceCode}' requires a natural resource code.");
    }

    public CityResourceEconomyRule ToRule() => new(
        NormalizedResourceCode,
        ConsumptionPerResident,
        BasePrice,
        TargetStockPerResident,
        CriticalStockRatio,
        SurplusStockRatio,
        MaximumPriceMultiplier,
        MinimumPriceMultiplier);

    private static string Normalize(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Resource code cannot be empty.", parameterName);
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > 80)
            throw new ArgumentException("Resource code cannot exceed 80 characters.", parameterName);
        return normalized;
    }
}
