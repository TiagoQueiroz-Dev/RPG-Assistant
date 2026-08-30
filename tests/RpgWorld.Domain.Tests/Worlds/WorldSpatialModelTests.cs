using RpgWorld.Domain.Worlds;
using RpgWorld.Modules.Default.Worlds;

namespace RpgWorld.Domain.Tests.Worlds;

public sealed class WorldSpatialModelTests
{
    [Fact]
    public void Converts_border_positions_to_the_expected_chunk()
    {
        var world = World.Create("Aster", width: 65, height: 33);

        Assert.Equal(3, world.ChunkColumns);
        Assert.Equal(2, world.ChunkRows);
        Assert.Equal(new ChunkCoordinate(0, 0), world.ChunkAt(world.PositionAt(0, 0)));
        Assert.Equal(new ChunkCoordinate(0, 0), world.ChunkAt(world.PositionAt(31, 31)));
        Assert.Equal(new ChunkCoordinate(1, 0), world.ChunkAt(world.PositionAt(32, 31)));
        Assert.Equal(new ChunkCoordinate(2, 1), world.ChunkAt(world.PositionAt(64, 32)));
    }

    [Fact]
    public void Rejects_positions_outside_world_or_from_another_world()
    {
        var world = World.Create("Aster", width: 64, height: 64);
        var otherWorld = World.Create("Other", width: 64, height: 64);

        Assert.Throws<ArgumentOutOfRangeException>(() => world.PositionAt(64, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.PositionAt(0, 64));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            world.ChunkAt(otherWorld.PositionAt(0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Position(world.Id, -1, 0));
    }

    [Fact]
    public void Creates_edge_chunk_and_tile_with_decoupled_entity_references()
    {
        var world = World.Create("Aster", width: 65, height: 33);
        var coordinate = new ChunkCoordinate(2, 1);
        var chunk = world.CreateChunk(coordinate);
        var position = world.PositionAt(64, 32);
        var tile = world.CreateTile(
            position,
            "Grassland",
            DefaultWorldDefinitions.Catalog,
            elevation: 42,
            temperatureCelsius: 18.5m,
            humidity: 0.65m);
        var actorId = Guid.NewGuid();

        tile.AddOccupant(actorId);
        tile.AddOccupant(actorId);
        tile.AssignResource(Guid.NewGuid());
        tile.AssignStructure(Guid.NewGuid());

        Assert.Equal(1, chunk.Width);
        Assert.Equal(1, chunk.Height);
        Assert.True(chunk.Contains(position));
        Assert.Equal(position, tile.Position);
        Assert.Equal("grassland", tile.BiomeCode);
        Assert.Equal("plains", tile.TerrainCode);
        Assert.Equal(actorId, Assert.Single(tile.OccupantIds));
    }
}
