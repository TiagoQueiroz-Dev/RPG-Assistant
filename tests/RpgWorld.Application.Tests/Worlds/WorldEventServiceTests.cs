using RpgWorld.Application.Worlds.Events;
using RpgWorld.Domain.Worlds.Events;

namespace RpgWorld.Application.Tests.Worlds;

public sealed class WorldEventServiceTests
{
    [Fact]
    public async Task Maps_persisted_json_to_a_master_timeline_dto()
    {
        var worldId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var item = WorldEvent.Create(
            Guid.NewGuid(), worldId, "ActorKilled", DateTimeOffset.UnixEpoch,
            null, [actorId], "{\"reason\":\"combat\"}");
        var repository = new FakeRepository(worldId, new WorldEventPage([item], 1, 20, 1));

        var result = await new WorldEventService(repository).SearchAsync(
            new WorldEventQuery(worldId, PageSize: 20, ActorId: actorId));

        var view = Assert.Single(result.Items);
        Assert.Equal("combat", view.Payload.GetProperty("reason").GetString());
        Assert.Equal([actorId], view.Actors);
        Assert.Equal(1, result.TotalPages);
        Assert.Equal(actorId, repository.LastQuery!.ActorId);
    }

    [Fact]
    public async Task Validates_pagination_period_and_complete_position()
    {
        var worldId = Guid.NewGuid();
        var service = new WorldEventService(new FakeRepository(
            worldId, new WorldEventPage([], 1, 20, 0)));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SearchAsync(new WorldEventQuery(worldId, Page: 0)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync(new WorldEventQuery(
            worldId, FromUtc: DateTimeOffset.UnixEpoch.AddDays(1), ToUtc: DateTimeOffset.UnixEpoch)));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SearchAsync(new WorldEventQuery(worldId, PositionX: 1)));
    }

    private sealed class FakeRepository(Guid worldId, WorldEventPage result) : IWorldEventRepository
    {
        public WorldEventQuery? LastQuery { get; private set; }
        public Task<bool> WorldExistsAsync(Guid candidate, CancellationToken cancellationToken = default) =>
            Task.FromResult(candidate == worldId);
        public Task<WorldEventPage> SearchAsync(WorldEventQuery query, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(result);
        }
    }
}
