namespace RpgWorld.Application.Caching;

public static class CacheKeys
{
    public static CacheKey ActiveChunk(Guid worldId, int x, int y) =>
        new($"active-chunks:{worldId:N}:{x}:{y}");

    public static CacheKey Session(Guid sessionId) =>
        new($"sessions:{sessionId:N}");

    public static CacheKey LoadedEntity(string entityType, Guid entityId) =>
        new($"loaded-entities:{Segment(entityType)}:{entityId:N}");

    public static CacheKey ReadModel(string modelName, string identifier) =>
        new($"read-models:{Segment(modelName)}:{Segment(identifier)}");

    private static string Segment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A cache key segment cannot be empty.", nameof(value));
        }

        return Uri.EscapeDataString(value.Trim().ToLowerInvariant());
    }
}

