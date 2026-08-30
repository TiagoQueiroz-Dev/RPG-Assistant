using Microsoft.Extensions.Logging.Abstractions;
using RpgWorld.Application.Caching;
using RpgWorld.Infrastructure.Caching;
using Testcontainers.Redis;

namespace RpgWorld.Infrastructure.Tests.Caching;

public sealed class RedisCacheServiceTests : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder("redis:7.4-alpine").Build();

    public Task InitializeAsync() => _redis.StartAsync();

    public Task DisposeAsync() => _redis.DisposeAsync().AsTask();

    [Fact]
    public async Task Supports_read_write_remove_expiration_and_durable_fallback()
    {
        await using var cache = new RedisCacheService(
            new RedisOptions(true, _redis.GetConnectionString(), "rpg-world-tests"),
            NullLogger<RedisCacheService>.Instance);

        var key = CacheKeys.LoadedEntity("npc", Guid.NewGuid());
        var shortExpiration = new CacheEntryOptions(TimeSpan.FromMilliseconds(400));
        var cachedValue = new CachedActor("Ayla", 12);

        await cache.SetAsync(key, cachedValue, shortExpiration);

        var immediate = await cache.GetAsync<CachedActor>(key);
        Assert.True(immediate.Found);
        Assert.Equal(cachedValue, immediate.Value);

        await Task.Delay(TimeSpan.FromMilliseconds(800));

        var durableReads = 0;
        var restored = await cache.GetOrLoadAsync(
            key,
            _ =>
            {
                durableReads++;
                return Task.FromResult(new CachedActor("Ayla", 13));
            },
            CachePolicy.For(CacheDataKind.LoadedEntity));

        Assert.Equal(1, durableReads);
        Assert.Equal(13, restored.Level);

        await cache.RemoveAsync(key);
        var removed = await cache.GetAsync<CachedActor>(key);
        Assert.False(removed.Found);
    }

    private sealed record CachedActor(string Name, int Level);
}

