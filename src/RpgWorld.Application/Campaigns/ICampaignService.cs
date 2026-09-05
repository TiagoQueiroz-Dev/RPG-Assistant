using System.Text.Json;

namespace RpgWorld.Application.Campaigns;

public sealed record CreateCampaignRequest(string Name, string ModuleId, string SettingsJson);
public sealed record CampaignView(Guid Id, Guid WorldId, string Name, string ModuleId,
    JsonElement Settings, string Status, DateTimeOffset CreatedAtUtc, DateTimeOffset? EndedAtUtc);

public interface ICampaignService
{
    Task<CampaignView> CreateAsync(Guid worldId, CreateCampaignRequest request, CancellationToken cancellationToken = default);
    Task<CampaignView> GetAsync(Guid worldId, Guid campaignId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CampaignView>> ListAsync(Guid worldId, int offset = 0, int limit = 50,
        CancellationToken cancellationToken = default);
    Task<CampaignView> EndAsync(Guid worldId, Guid campaignId, CancellationToken cancellationToken = default);
}
