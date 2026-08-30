using RpgWorld.Application.Caching;

namespace RpgWorld.Application.Tests.Caching;

public sealed class CacheAsideExtensionsTests
{
    [Fact]
    public async Task Miss_loads_from_durable_source_and_populates_cache()
    {
        var cache = new RecordingCacheService();
        var durableReads = 0;
        var key = CacheKeys.ReadModel("world-summary", "world-1");

        var value = await cache.GetOrLoadAsync(
            key,
            _ =>
            {
                durableReads++;
                return Task.FromResult("persisted-value");
            },
            CachePolicy.For(CacheDataKind.ReadModel));

        Assert.Equal("persisted-value", value);
        Assert.Equal(1, durableReads);
        Assert.Equal("persisted-value", cache.StoredValue);
    }

    [Fact]
    public async Task Hit_does_not_query_durable_source()
    {
        var cache = new RecordingCacheService { StoredValue = "cached-value" };
        var durableReads = 0;

        var value = await cache.GetOrLoadAsync(
            CacheKeys.Session(Guid.NewGuid()),
            _ =>
            {
                durableReads++;
                return Task.FromResult("persisted-value");
            },
            CachePolicy.For(CacheDataKind.Session));

        Assert.Equal("cached-value", value);
        Assert.Equal(0, durableReads);
    }

    [Fact]
    public void Key_factories_create_namespaced_deterministic_keys()
    {
        var worldId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        Assert.Equal(
            "active-chunks:aaaaaaaabbbbccccddddeeeeeeeeeeee:-2:7",
            CacheKeys.ActiveChunk(worldId, -2, 7).Value);
        Assert.Equal(
            "loaded-entities:npc:aaaaaaaabbbbccccddddeeeeeeeeeeee",
            CacheKeys.LoadedEntity("NPC", worldId).Value);
    }

    private sealed class RecordingCacheService : ICacheService
    {
        public string? StoredValue { get; set; }

        public Task<CacheReadResult<T>> GetAsync<T>(
            CacheKey key,
            CancellationToken cancellationToken = default)
            where T : notnull
        {
            var result = StoredValue is T value
                ? CacheReadResult<T>.Hit(value)
                : CacheReadResult<T>.Miss();

            return Task.FromResult(result);
        }

        public Task SetAsync<T>(
            CacheKey key,
            T value,
            CacheEntryOptions options,
            CancellationToken cancellationToken = default)
            where T : notnull
        {
            StoredValue = value as string;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(
            CacheKey key,
            CancellationToken cancellationToken = default)
        {
            StoredValue = null;
            return Task.CompletedTask;
        }
    }
}

