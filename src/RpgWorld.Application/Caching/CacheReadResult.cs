namespace RpgWorld.Application.Caching;

public readonly record struct CacheReadResult<T>
    where T : notnull
{
    private CacheReadResult(bool found, T? value)
    {
        Found = found;
        Value = value;
    }

    public bool Found { get; }

    public T? Value { get; }

    public static CacheReadResult<T> Miss() => new(false, default);

    public static CacheReadResult<T> Hit(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new CacheReadResult<T>(true, value);
    }
}

