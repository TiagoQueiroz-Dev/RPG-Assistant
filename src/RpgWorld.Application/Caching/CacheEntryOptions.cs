namespace RpgWorld.Application.Caching;

public sealed record CacheEntryOptions
{
    public CacheEntryOptions(TimeSpan absoluteExpiration)
    {
        if (absoluteExpiration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(absoluteExpiration),
                "Cache expiration must be positive.");
        }

        AbsoluteExpiration = absoluteExpiration;
    }

    public TimeSpan AbsoluteExpiration { get; }
}

public enum CacheDataKind
{
    ActiveChunk,
    Session,
    LoadedEntity,
    ReadModel
}

public static class CachePolicy
{
    public static CacheEntryOptions For(CacheDataKind kind) => kind switch
    {
        CacheDataKind.ActiveChunk => new(TimeSpan.FromMinutes(5)),
        CacheDataKind.Session => new(TimeSpan.FromMinutes(30)),
        CacheDataKind.LoadedEntity => new(TimeSpan.FromMinutes(10)),
        CacheDataKind.ReadModel => new(TimeSpan.FromMinutes(2)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}

