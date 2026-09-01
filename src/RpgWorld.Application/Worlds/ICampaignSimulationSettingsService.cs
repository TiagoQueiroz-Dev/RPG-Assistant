namespace RpgWorld.Application.Worlds;

public sealed record CampaignSimulationSettingsView(
    Guid WorldId,
    decimal NPCDensity,
    decimal CreatureSpawnRate,
    decimal WarFrequency,
    decimal EconomicDifficulty,
    decimal ResourceScarcity,
    decimal MigrationRate,
    decimal PopulationGrowth,
    decimal SimulationSpeed,
    int Version,
    DateTimeOffset UpdatedAtUtc);

public sealed record UpdateCampaignSimulationSettings(
    decimal NPCDensity,
    decimal CreatureSpawnRate,
    decimal WarFrequency,
    decimal EconomicDifficulty,
    decimal ResourceScarcity,
    decimal MigrationRate,
    decimal PopulationGrowth,
    decimal SimulationSpeed);

public interface ICampaignSimulationSettingsProvider
{
    Task<CampaignSimulationSettingsView> GetEffectiveAsync(Guid worldId, CancellationToken cancellationToken = default);
}

public interface ICampaignSimulationSettingsService : ICampaignSimulationSettingsProvider
{
    Task<CampaignSimulationSettingsView> UpdateAsync(
        Guid worldId, UpdateCampaignSimulationSettings settings, CancellationToken cancellationToken = default);
}
