using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Campaigns;
using RpgWorld.Domain.Campaigns;
using RpgWorld.Infrastructure.Persistence;
using RpgWorld.Modules.Abstractions;

namespace RpgWorld.Infrastructure.Campaigns;

public sealed class CampaignService(RpgWorldDbContext dbContext, IRpgModuleCatalog modules,
    TimeProvider timeProvider) : ICampaignService
{
    public async Task<CampaignView> CreateAsync(Guid worldId, CreateCampaignRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var world = await dbContext.Worlds.AsNoTracking().SingleOrDefaultAsync(value => value.Id == worldId, cancellationToken)
            ?? throw new KeyNotFoundException($"World '{worldId}' was not found.");
        var module = modules.AvailableModules.SingleOrDefault(value =>
            string.Equals(value.Id, request.ModuleId?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Module '{request.ModuleId}' is not available.", nameof(request));
        // Resolve the content now so an unusable module cannot create a playable campaign.
        modules.Load([module.Id]);
        var campaign = Campaign.Create(world, request.Name, module.Id, request.SettingsJson, timeProvider.GetUtcNow());
        dbContext.Campaigns.Add(campaign);
        await dbContext.SaveChangesAsync(cancellationToken);
        return View(campaign);
    }

    public async Task<CampaignView> GetAsync(Guid worldId, Guid campaignId,
        CancellationToken cancellationToken = default) => View(await RequiredAsync(worldId, campaignId, cancellationToken));

    public async Task<IReadOnlyList<CampaignView>> ListAsync(Guid worldId, int offset = 0, int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (offset < 0 || limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        if (!await dbContext.Worlds.AsNoTracking().AnyAsync(value => value.Id == worldId, cancellationToken))
            throw new KeyNotFoundException($"World '{worldId}' was not found.");
        var campaigns = await dbContext.Campaigns.AsNoTracking().Where(value => value.WorldId == worldId)
            .OrderByDescending(value => value.CreatedAtUtc).ThenBy(value => value.Id)
            .Skip(offset).Take(limit).ToArrayAsync(cancellationToken);
        return campaigns.Select(View).ToArray();
    }

    public async Task<CampaignView> EndAsync(Guid worldId, Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        var campaign = await dbContext.Campaigns.SingleOrDefaultAsync(
            value => value.WorldId == worldId && value.Id == campaignId, cancellationToken)
            ?? throw new KeyNotFoundException($"Campaign '{campaignId}' was not found in this world.");
        campaign.End(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return View(campaign);
    }

    private async Task<Campaign> RequiredAsync(Guid worldId, Guid campaignId, CancellationToken cancellationToken) =>
        await dbContext.Campaigns.AsNoTracking().SingleOrDefaultAsync(
            value => value.WorldId == worldId && value.Id == campaignId, cancellationToken)
        ?? throw new KeyNotFoundException($"Campaign '{campaignId}' was not found in this world.");

    private static CampaignView View(Campaign campaign)
    {
        using var settings = JsonDocument.Parse(campaign.SettingsJson);
        return new(campaign.Id, campaign.WorldId, campaign.Name, campaign.ModuleId, settings.RootElement.Clone(),
            campaign.Status.ToString(), campaign.CreatedAtUtc, campaign.EndedAtUtc);
    }
}
