namespace RpgWorld.Application.Caching;

public static class CacheAsideExtensions
{
    public static async Task<T> GetOrLoadAsync<T>(
        this ICacheService cache,
        CacheKey key,
        Func<CancellationToken, Task<T>> loadFromSourceOfTruth,
        CacheEntryOptions options,
        CancellationToken cancellationToken = default)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(loadFromSourceOfTruth);
        ArgumentNullException.ThrowIfNull(options);

        var cached = await cache.GetAsync<T>(key, cancellationToken);
        if (cached.Found)
        {
            return cached.Value!;
        }

        var durableValue = await loadFromSourceOfTruth(cancellationToken);
        ArgumentNullException.ThrowIfNull(durableValue);

        await cache.SetAsync(key, durableValue, options, cancellationToken);
        return durableValue;
    }
}

