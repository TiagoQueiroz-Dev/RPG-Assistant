using RpgWorld.Application.Worlds.Admin;

namespace RpgWorld.Application.Tests.Worlds;

public sealed class WorldAdminServiceTests
{
    [Fact]
    public async Task Normalizes_entity_filter_and_enforces_bounded_pagination()
    {
        var worldId = Guid.NewGuid();
        var repository = new FakeRepository(worldId);
        var service = new WorldAdminService(repository);

        await service.InspectAsync(new WorldAdminQuery(worldId, " NPCS ", PageSize: 20));

        Assert.Equal("npcs", repository.LastQuery!.EntityType);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.InspectAsync(new WorldAdminQuery(worldId, PageSize: 201)));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.InspectAsync(new WorldAdminQuery(worldId, "unknown")));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.InspectAsync(new WorldAdminQuery(worldId, RegionX: 1)));
    }

    [Fact]
    public async Task Map_layer_requests_require_one_isolated_bounded_viewport()
    {
        var worldId = Guid.NewGuid();
        var repository = new FakeLayerRepository(worldId);
        var service = new WorldMapLayerService(repository);

        var result = await service.LoadAsync(new WorldMapLayerQuery(
            worldId, WorldMapLayerMode.Population, 0, 0, 31, 31));

        Assert.Equal("Population", result.Mode);
        Assert.Equal(WorldMapLayerMode.Population, repository.LastQuery!.Mode);
        await Assert.ThrowsAsync<ArgumentException>(() => service.LoadAsync(new WorldMapLayerQuery(
            worldId, WorldMapLayerMode.Economy, MinX: 0)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.LoadAsync(new WorldMapLayerQuery(
            worldId, WorldMapLayerMode.Resources, 0, 0, 1000, 1000)));
    }

    private sealed class FakeRepository(Guid worldId) : IWorldAdminRepository
    {
        public WorldAdminQuery? LastQuery { get; private set; }
        public Task<WorldAdminView?> InspectAsync(WorldAdminQuery query, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult<WorldAdminView?>(query.WorldId != worldId ? null : new WorldAdminView(
                worldId, "World", true, null, new(8, 8, 4, 64, 4),
                new(0, 0, 0, 0, 0, 0, 0m, 0, 0, 0m, 0, 0, 0m, 0, 0),
                query.EntityType, [], query.Page, query.PageSize, 0, 0, WorldAdminService.EntityTypes));
        }
    }

    private sealed class FakeLayerRepository(Guid worldId) : IWorldMapLayerRepository
    {
        public WorldMapLayerQuery? LastQuery { get; private set; }
        public Task<WorldMapLayerView?> LoadAsync(WorldMapLayerQuery query, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult<WorldMapLayerView?>(query.WorldId == worldId
                ? new(worldId, query.Mode.ToString(), DateTimeOffset.UnixEpoch, [], [])
                : null);
        }
    }
}
