using RpgWorld.Domain.Worlds;

namespace RpgWorld.Domain.Tests.Worlds;

public sealed class WorldSimulationStateTests
{
    [Fact]
    public void New_world_runs_by_default_and_can_be_paused_and_restarted()
    {
        var world = World.Create("Persistent realm", 32, 32);

        Assert.True(world.IsSimulationRunning);

        world.PauseSimulation();
        Assert.False(world.IsSimulationRunning);

        world.StartSimulation();
        Assert.True(world.IsSimulationRunning);
    }
}
