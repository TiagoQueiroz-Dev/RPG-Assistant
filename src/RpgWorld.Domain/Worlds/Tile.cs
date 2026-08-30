using RpgWorld.Domain.Worlds.Definitions;

namespace RpgWorld.Domain.Worlds;

public sealed class Tile
{
    private Guid[] _occupantIds = [];

    private Tile()
    {
    }

    private Tile(
        Guid id,
        Position position,
        TerrainDefinition terrain,
        BiomeDefinition biome,
        short elevation,
        decimal temperatureCelsius,
        decimal humidity,
        BiomeClassificationOrigin classificationOrigin,
        decimal? classificationConfidence)
    {
        Id = id;
        WorldId = position.WorldId;
        X = position.X;
        Y = position.Y;
        TerrainCode = terrain.Code;
        BiomeCode = biome.Code;
        Elevation = elevation;
        SetClassification(classificationOrigin, classificationConfidence);
        SetClimate(temperatureCelsius, humidity);
    }

    public Guid Id { get; private set; }

    public Guid WorldId { get; private set; }

    public int X { get; private set; }

    public int Y { get; private set; }

    public string TerrainCode { get; private set; } = string.Empty;

    public string BiomeCode { get; private set; } = string.Empty;

    public short Elevation { get; private set; }

    public decimal TemperatureCelsius { get; private set; }

    public decimal Humidity { get; private set; }

    public BiomeClassificationOrigin BiomeClassificationOrigin { get; private set; }

    public decimal? BiomeClassificationConfidence { get; private set; }

    public Guid? ResourceDepositId { get; private set; }

    public Guid? StructureId { get; private set; }

    public Position Position => new(WorldId, X, Y);

    public IReadOnlyList<Guid> OccupantIds => _occupantIds;

    internal static Tile Create(
        Position position,
        TerrainDefinition terrain,
        BiomeDefinition biome,
        short elevation,
        decimal temperatureCelsius,
        decimal humidity,
        BiomeClassificationOrigin classificationOrigin = BiomeClassificationOrigin.Manual,
        decimal? classificationConfidence = null) =>
        new(
            Guid.CreateVersion7(),
            position,
            terrain,
            biome,
            elevation,
            temperatureCelsius,
            humidity,
            classificationOrigin,
            classificationConfidence);

    public void SetEnvironment(
        string biomeCode,
        IWorldDefinitionCatalog definitions,
        short elevation,
        decimal temperatureCelsius,
        decimal humidity)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var biome = definitions.ResolveBiome(biomeCode);
        var terrain = definitions.ResolveTerrain(biome.TerrainCode);
        TerrainCode = terrain.Code;
        BiomeCode = biome.Code;
        SetClassification(BiomeClassificationOrigin.Manual, null);
        Elevation = elevation;
        SetClimate(temperatureCelsius, humidity);
    }

    public bool ApplyAutomaticClassification(
        string biomeCode,
        IWorldDefinitionCatalog definitions,
        decimal confidence)
    {
        if (BiomeClassificationOrigin == BiomeClassificationOrigin.Manual)
        {
            return false;
        }

        ArgumentNullException.ThrowIfNull(definitions);
        var biome = definitions.ResolveBiome(biomeCode);
        var terrain = definitions.ResolveTerrain(biome.TerrainCode);
        TerrainCode = terrain.Code;
        BiomeCode = biome.Code;
        SetClassification(BiomeClassificationOrigin.Automatic, confidence);
        return true;
    }

    public void AssignResource(Guid? resourceDepositId) =>
        ResourceDepositId = OptionalId(resourceDepositId, nameof(resourceDepositId));

    public void AssignStructure(Guid? structureId) =>
        StructureId = OptionalId(structureId, nameof(structureId));

    public void AddOccupant(Guid actorId)
    {
        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("Actor identifier cannot be empty.", nameof(actorId));
        }

        if (!_occupantIds.Contains(actorId))
        {
            _occupantIds = [.. _occupantIds, actorId];
        }
    }

    public void RemoveOccupant(Guid actorId) =>
        _occupantIds = _occupantIds.Where(id => id != actorId).ToArray();

    private void SetClimate(decimal temperatureCelsius, decimal humidity)
    {
        if (temperatureCelsius is < -150 or > 150)
        {
            throw new ArgumentOutOfRangeException(
                nameof(temperatureCelsius),
                "Temperature must be between -150°C and 150°C.");
        }

        if (humidity is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(humidity),
                "Humidity must be between 0 and 1.");
        }

        TemperatureCelsius = temperatureCelsius;
        Humidity = humidity;
    }

    private void SetClassification(
        BiomeClassificationOrigin origin,
        decimal? confidence)
    {
        if (origin == BiomeClassificationOrigin.Automatic &&
            (confidence is null or < 0 or > 1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(confidence),
                "Automatic classification confidence must be between 0 and 1.");
        }

        BiomeClassificationOrigin = origin;
        BiomeClassificationConfidence = origin == BiomeClassificationOrigin.Manual
            ? null
            : confidence;
    }

    private static Guid? OptionalId(Guid? value, string parameterName) =>
        value == Guid.Empty
            ? throw new ArgumentException("Identifier cannot be empty.", parameterName)
            : value;
}
