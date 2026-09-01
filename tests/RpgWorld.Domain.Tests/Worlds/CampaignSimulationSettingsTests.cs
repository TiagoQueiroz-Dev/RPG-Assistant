using RpgWorld.Domain.Worlds;

namespace RpgWorld.Domain.Tests.Worlds;

public sealed class CampaignSimulationSettingsTests
{
    [Fact]
    public void Defaults_are_balanced_and_versioned()
    {
        var settings = CampaignSimulationSettings.CreateDefault(Guid.NewGuid(), DateTimeOffset.UnixEpoch);

        Assert.Equal(1m, settings.NPCDensity);
        Assert.Equal(1m, settings.ResourceScarcity);
        Assert.Equal(1m, settings.SimulationSpeed);
        Assert.Equal(1, settings.Version);
    }

    [Fact]
    public void Update_rejects_values_outside_declared_limits()
    {
        var settings = CampaignSimulationSettings.CreateDefault(Guid.NewGuid(), DateTimeOffset.UnixEpoch);

        Assert.Throws<ArgumentOutOfRangeException>(() => settings.Update(
            0m, 1m, 1m, 1m, 1m, 1m, 1m, 1m, DateTimeOffset.UnixEpoch));
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.Update(
            1m, 1m, 1m, 1m, 1m, 1m, 1m, 101m, DateTimeOffset.UnixEpoch));
        Assert.Equal((1m, 1m, 1), (settings.NPCDensity, settings.ResourceScarcity, settings.Version));
    }
}
