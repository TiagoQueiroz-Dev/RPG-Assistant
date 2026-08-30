using RpgWorld.Domain.Worlds;

namespace RpgWorld.Domain.Tests.Worlds;

public sealed class RegionAggregateStateTests
{
    [Fact]
    public void Chunk_preserves_aggregate_state_across_all_levels()
    {
        var world = World.Create("Levels", 32, 32);
        var chunk = world.CreateChunk(new ChunkCoordinate(0, 0));
        var aggregate = new RegionAggregateState(120, 45.5m, 12m, 18m);

        chunk.TransitionSimulationLevel(SimulationLevel.Regional, aggregate);
        chunk.TransitionSimulationLevel(SimulationLevel.Detailed, chunk.GetAggregateState());

        Assert.Equal(SimulationLevel.Detailed, chunk.SimulationLevel);
        Assert.Equal(aggregate, chunk.GetAggregateState());
        Assert.True(chunk.AllowsIndividualActions);
    }

    [Fact]
    public void Aggregate_values_cannot_be_negative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RegionAggregateState(-1, 0, 0, 0));
    }
}
