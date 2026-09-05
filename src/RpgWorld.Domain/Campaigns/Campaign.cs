using System.Text.Json;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Domain.Campaigns;

public enum CampaignStatus { Active, Ended }

public sealed class Campaign
{
    private Campaign() { }

    public Guid Id { get; private set; }
    public Guid WorldId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string ModuleId { get; private set; } = string.Empty;
    public string SettingsJson { get; private set; } = "{}";
    public CampaignStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? EndedAtUtc { get; private set; }

    public static Campaign Create(World world, string name, string moduleId, string settingsJson,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new ArgumentException("Campaign name must contain 1 to 200 characters.", nameof(name));
        if (string.IsNullOrWhiteSpace(moduleId) || moduleId.Trim().Length > 200)
            throw new ArgumentException("A module identifier of up to 200 characters is required.", nameof(moduleId));
        if (string.IsNullOrWhiteSpace(settingsJson) || settingsJson.Length > 16_384)
            throw new ArgumentException("Campaign settings must be a JSON object of up to 16384 characters.", nameof(settingsJson));
        using var settings = JsonDocument.Parse(settingsJson);
        if (settings.RootElement.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Campaign settings must be a JSON object.", nameof(settingsJson));
        return new Campaign
        {
            Id = Guid.CreateVersion7(), WorldId = world.Id, Name = name.Trim(),
            ModuleId = moduleId.Trim().ToLowerInvariant(), SettingsJson = settings.RootElement.GetRawText(),
            Status = CampaignStatus.Active, CreatedAtUtc = createdAtUtc.ToUniversalTime()
        };
    }

    public void End(DateTimeOffset endedAtUtc)
    {
        if (Status == CampaignStatus.Ended) return;
        if (endedAtUtc < CreatedAtUtc)
            throw new ArgumentOutOfRangeException(nameof(endedAtUtc), "Campaign cannot end before its creation.");
        Status = CampaignStatus.Ended;
        EndedAtUtc = endedAtUtc.ToUniversalTime();
    }
}
