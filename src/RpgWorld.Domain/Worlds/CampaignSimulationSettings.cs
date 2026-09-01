namespace RpgWorld.Domain.Worlds;

public sealed class CampaignSimulationSettings
{
    private CampaignSimulationSettings() { }
    private CampaignSimulationSettings(Guid worldId, DateTimeOffset createdAtUtc)
    {
        if (worldId == Guid.Empty) throw new ArgumentException("World identifier is required.", nameof(worldId));
        WorldId = worldId;
        NPCDensity = 1m;
        CreatureSpawnRate = 1m;
        WarFrequency = 1m;
        EconomicDifficulty = 1m;
        ResourceScarcity = 1m;
        MigrationRate = 1m;
        PopulationGrowth = 1m;
        SimulationSpeed = 1m;
        Version = 1;
        UpdatedAtUtc = createdAtUtc.ToUniversalTime();
    }

    public Guid WorldId { get; private set; }
    public decimal NPCDensity { get; private set; }
    public decimal CreatureSpawnRate { get; private set; }
    public decimal WarFrequency { get; private set; }
    public decimal EconomicDifficulty { get; private set; }
    public decimal ResourceScarcity { get; private set; }
    public decimal MigrationRate { get; private set; }
    public decimal PopulationGrowth { get; private set; }
    public decimal SimulationSpeed { get; private set; }
    public int Version { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static CampaignSimulationSettings CreateDefault(Guid worldId, DateTimeOffset createdAtUtc) => new(worldId, createdAtUtc);

    public void Update(
        decimal npcDensity, decimal creatureSpawnRate, decimal warFrequency,
        decimal economicDifficulty, decimal resourceScarcity, decimal migrationRate,
        decimal populationGrowth, decimal simulationSpeed, DateTimeOffset updatedAtUtc)
    {
        var validatedNpcDensity = InRange(npcDensity, 0.1m, 5m, nameof(npcDensity));
        var validatedCreatureSpawnRate = InRange(creatureSpawnRate, 0m, 10m, nameof(creatureSpawnRate));
        var validatedWarFrequency = InRange(warFrequency, 0m, 10m, nameof(warFrequency));
        var validatedEconomicDifficulty = InRange(economicDifficulty, 0.1m, 5m, nameof(economicDifficulty));
        var validatedResourceScarcity = InRange(resourceScarcity, 0.1m, 10m, nameof(resourceScarcity));
        var validatedMigrationRate = InRange(migrationRate, 0m, 5m, nameof(migrationRate));
        var validatedPopulationGrowth = InRange(populationGrowth, 0m, 5m, nameof(populationGrowth));
        var validatedSimulationSpeed = InRange(simulationSpeed, 0m, 100m, nameof(simulationSpeed));

        NPCDensity = validatedNpcDensity;
        CreatureSpawnRate = validatedCreatureSpawnRate;
        WarFrequency = validatedWarFrequency;
        EconomicDifficulty = validatedEconomicDifficulty;
        ResourceScarcity = validatedResourceScarcity;
        MigrationRate = validatedMigrationRate;
        PopulationGrowth = validatedPopulationGrowth;
        SimulationSpeed = validatedSimulationSpeed;
        Version = checked(Version + 1);
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
    }

    private static decimal InRange(decimal value, decimal minimum, decimal maximum, string name) =>
        value < minimum || value > maximum
            ? throw new ArgumentOutOfRangeException(name, $"{name} must be between {minimum} and {maximum}.")
            : value;
}
