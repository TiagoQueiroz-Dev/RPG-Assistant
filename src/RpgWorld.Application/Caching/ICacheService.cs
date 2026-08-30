namespace RpgWorld.Application.Caching;

public interface ICacheService
{
    Task<CacheReadResult<T>> GetAsync<T>(
        CacheKey key,
        CancellationToken cancellationToken = default)
        where T : notnull;

    Task SetAsync<T>(
        CacheKey key,
        T value,
        CacheEntryOptions options,
        CancellationToken cancellationToken = default)
        where T : notnull;

    Task RemoveAsync(
        CacheKey key,
        CancellationToken cancellationToken = default);
}

