namespace RpgWorld.Application.Caching;

public readonly record struct CacheKey
{
    public CacheKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A cache key cannot be empty.", nameof(value));
        }

        if (value.Length > 256)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "A cache key cannot exceed 256 characters.");
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

