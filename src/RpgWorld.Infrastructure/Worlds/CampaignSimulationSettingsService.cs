using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Worlds;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Events;
using RpgWorld.Infrastructure.Persistence;

namespace RpgWorld.Infrastructure.Worlds;

public sealed class CampaignSimulationSettingsService(
    RpgWorldDbContext dbContext,
    TimeProvider timeProvider) : ICampaignSimulationSettingsService
{
    public async Task<CampaignSimulationSettingsView> GetEffectiveAsync(
        Guid worldId, CancellationToken cancellationToken = default)
    {
        await RequireWorldAsync(worldId, cancellationToken);
        var settings = await dbContext.CampaignSimulationSettings.AsNoTracking()
            .SingleOrDefaultAsync(value => value.WorldId == worldId, cancellationToken);
        return View(settings ?? CampaignSimulationSettings.CreateDefault(worldId, timeProvider.GetUtcNow()));
    }

    public async Task<CampaignSimulationSettingsView> UpdateAsync(
        Guid worldId,
        UpdateCampaignSimulationSettings request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await RequireWorldAsync(worldId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var settings = await dbContext.CampaignSimulationSettings
            .SingleOrDefaultAsync(value => value.WorldId == worldId, cancellationToken);
        if (settings is null)
        {
            settings = CampaignSimulationSettings.CreateDefault(worldId, now);
            dbContext.CampaignSimulationSettings.Add(settings);
        }
        settings.Update(request.NPCDensity, request.CreatureSpawnRate, request.WarFrequency,
            request.EconomicDifficulty, request.ResourceScarcity, request.MigrationRate,
            request.PopulationGrowth, request.SimulationSpeed, now);
        var clock = await dbContext.WorldClocks.SingleOrDefaultAsync(value => value.WorldId == worldId, cancellationToken);
        if (clock is null)
        {
            clock = WorldClock.Create(worldId, now, now, realTimeMultiplier: settings.SimulationSpeed);
            dbContext.WorldClocks.Add(clock);
        }
        else
        {
            clock.SetRealTimeMultiplier(settings.SimulationSpeed);
        }
        var result = View(settings);
        var eventId = Guid.CreateVersion7();
        dbContext.WorldEvents.Add(WorldEvent.Create(eventId, worldId, "campaign.settings.changed", now,
            null, [], JsonSerializer.Serialize(result), correlationId: eventId));
        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    private async Task RequireWorldAsync(Guid worldId, CancellationToken cancellationToken)
    {
        if (worldId == Guid.Empty || !await dbContext.Worlds.AsNoTracking()
                .AnyAsync(value => value.Id == worldId, cancellationToken))
            throw new KeyNotFoundException($"World '{worldId}' was not found.");
    }

    private static CampaignSimulationSettingsView View(CampaignSimulationSettings value) =>
        new(value.WorldId, value.NPCDensity, value.CreatureSpawnRate, value.WarFrequency,
            value.EconomicDifficulty, value.ResourceScarcity, value.MigrationRate,
            value.PopulationGrowth, value.SimulationSpeed, value.Version, value.UpdatedAtUtc);
}
