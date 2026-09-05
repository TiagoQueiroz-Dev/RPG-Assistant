using RpgWorld.Domain.Campaigns;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Domain.Tests.Campaigns;

public sealed class CampaignTests
{
    [Fact]
    public void Campaign_has_independent_lifecycle_and_session_settings()
    {
        var world = World.Create("World", 8, 8, 4);
        var created = DateTimeOffset.UnixEpoch;
        var campaign = Campaign.Create(world, "  Evening game  ", "RPGWORLD.DEFAULT", "{\"language\":\"pt-BR\"}", created);
        Assert.Equal((world.Id, "Evening game", "rpgworld.default"), (campaign.WorldId, campaign.Name, campaign.ModuleId));
        Assert.Equal(CampaignStatus.Active, campaign.Status);
        Assert.Throws<ArgumentOutOfRangeException>(() => campaign.End(created.AddSeconds(-1)));
        campaign.End(created.AddHours(2));
        campaign.End(created.AddHours(3));
        Assert.Equal(CampaignStatus.Ended, campaign.Status);
        Assert.Equal(created.AddHours(2), campaign.EndedAtUtc);
        Assert.True(world.IsSimulationRunning);
    }

    [Theory]
    [InlineData("", "module", "{}")]
    [InlineData("Game", "", "{}")]
    [InlineData("Game", "module", "[]")]
    [InlineData("Game", "module", "null")]
    public void Invalid_campaign_metadata_is_rejected(string name, string module, string settings)
    {
        Assert.Throws<ArgumentException>(() => Campaign.Create(World.Create("World", 8, 8, 4),
            name, module, settings, DateTimeOffset.UnixEpoch));
    }
}
