using RpgWorld.Application.Caching;

namespace RpgWorld.Infrastructure.Caching;

internal sealed class NoOpCacheService : ICacheService
{
    public Task<CacheReadResult<T>> GetAsync<T>(
        CacheKey key,
        CancellationToken cancellationToken = default)
        where T : notnull =>
        Task.FromResult(CacheReadResult<T>.Miss());

    public Task SetAsync<T>(
        CacheKey key,
        T value,
        CacheEntryOptions options,
        CancellationToken cancellationToken = default)
        where T : notnull =>
        Task.CompletedTask;

    public Task RemoveAsync(
        CacheKey key,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

