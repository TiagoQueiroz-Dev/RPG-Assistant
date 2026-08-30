namespace RpgWorld.Domain.Worlds.Definitions;

public sealed class BiomeDefinition
{
    public BiomeDefinition(
        string code,
        string name,
        string terrainCode,
        decimal minimumTemperatureCelsius,
        decimal maximumTemperatureCelsius,
        decimal minimumHumidity,
        decimal maximumHumidity,
        decimal movementCostMultiplier = 1m,
        IEnumerable<string>? resourceTags = null,
        IEnumerable<string>? spawnTags = null)
    {
        ValidateRange(
            minimumTemperatureCelsius,
            maximumTemperatureCelsius,
            -150m,
            150m,
            nameof(minimumTemperatureCelsius),
            nameof(maximumTemperatureCelsius));
        ValidateRange(
            minimumHumidity,
            maximumHumidity,
            0m,
            1m,
            nameof(minimumHumidity),
            nameof(maximumHumidity));

        if (movementCostMultiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(movementCostMultiplier),
                "Movement cost multiplier must be greater than zero.");
        }

        Code = DefinitionCode.Normalize(code, nameof(code));
        Name = DefinitionCode.RequiredName(name, nameof(name));
        TerrainCode = DefinitionCode.Normalize(terrainCode, nameof(terrainCode));
        MinimumTemperatureCelsius = minimumTemperatureCelsius;
        MaximumTemperatureCelsius = maximumTemperatureCelsius;
        MinimumHumidity = minimumHumidity;
        MaximumHumidity = maximumHumidity;
        MovementCostMultiplier = movementCostMultiplier;
        ResourceTags = DefinitionCode.NormalizeTags(resourceTags, nameof(resourceTags));
        SpawnTags = DefinitionCode.NormalizeTags(spawnTags, nameof(spawnTags));
    }

    public string Code { get; }

    public string Name { get; }

    public string TerrainCode { get; }

    public decimal MinimumTemperatureCelsius { get; }

    public decimal MaximumTemperatureCelsius { get; }

    public decimal MinimumHumidity { get; }

    public decimal MaximumHumidity { get; }

    public decimal MovementCostMultiplier { get; }

    public IReadOnlySet<string> ResourceTags { get; }

    public IReadOnlySet<string> SpawnTags { get; }

    public bool SupportsClimate(decimal temperatureCelsius, decimal humidity) =>
        temperatureCelsius >= MinimumTemperatureCelsius &&
        temperatureCelsius <= MaximumTemperatureCelsius &&
        humidity >= MinimumHumidity &&
        humidity <= MaximumHumidity;

    private static void ValidateRange(
        decimal minimum,
        decimal maximum,
        decimal allowedMinimum,
        decimal allowedMaximum,
        string minimumParameter,
        string maximumParameter)
    {
        if (minimum < allowedMinimum || minimum > allowedMaximum)
        {
            throw new ArgumentOutOfRangeException(minimumParameter);
        }

        if (maximum < allowedMinimum || maximum > allowedMaximum)
        {
            throw new ArgumentOutOfRangeException(maximumParameter);
        }

        if (minimum > maximum)
        {
            throw new ArgumentException("Minimum value cannot be greater than maximum value.", minimumParameter);
        }
    }
}
