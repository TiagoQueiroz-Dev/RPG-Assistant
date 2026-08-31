namespace RpgWorld.Application.Worlds.Admin;

public sealed class WorldAdminService(IWorldAdminRepository repository) : IWorldAdminService
{
    public static readonly IReadOnlyList<string> EntityTypes =
        ["chunks", "npcs", "players", "creatures", "resources", "cities", "factions", "armies"];

    public async Task<WorldAdminView> InspectAsync(
        WorldAdminQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.WorldId == Guid.Empty) throw new ArgumentException("World identifier is required.", nameof(query));
        var entityType = query.EntityType.Trim().ToLowerInvariant();
        if (!EntityTypes.Contains(entityType)) throw new ArgumentException("Unknown administrative entity type.", nameof(query));
        if (query.Page <= 0) throw new ArgumentOutOfRangeException(nameof(query.Page));
        if (query.PageSize is <= 0 or > 200) throw new ArgumentOutOfRangeException(nameof(query.PageSize));
        if (query.RegionX < 0 || query.RegionY < 0 || query.RegionX.HasValue != query.RegionY.HasValue)
            throw new ArgumentException("Region filter requires valid X and Y coordinates.", nameof(query));
        if (query.FactionId == Guid.Empty) throw new ArgumentException("Faction filter cannot be empty.", nameof(query));
        return await repository.InspectAsync(query with { EntityType = entityType }, cancellationToken)
            ?? throw new KeyNotFoundException($"World '{query.WorldId}' was not found.");
    }
}
