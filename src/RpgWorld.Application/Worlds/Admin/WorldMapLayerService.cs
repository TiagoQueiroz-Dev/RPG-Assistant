namespace RpgWorld.Application.Worlds.Admin;

public sealed class WorldMapLayerService(IWorldMapLayerRepository repository) : IWorldMapLayerService
{
    public const int MaximumCellsPerRequest = 100_000;

    public async Task<WorldMapLayerView> LoadAsync(
        WorldMapLayerQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.WorldId == Guid.Empty) throw new ArgumentException("World identifier is required.", nameof(query));
        if (!Enum.IsDefined(query.Mode)) throw new ArgumentOutOfRangeException(nameof(query));
        var bounds = new[] { query.MinX, query.MinY, query.MaxX, query.MaxY };
        if (query.Mode != WorldMapLayerMode.Normal && bounds.All(value => !value.HasValue))
            throw new ArgumentException("Analytical layers require a bounded viewport.", nameof(query));
        if (bounds.Any(value => value.HasValue) && bounds.Any(value => !value.HasValue))
            throw new ArgumentException("Layer viewport requires all four bounds.", nameof(query));
        if (query.MinX < 0 || query.MinY < 0 || query.MaxX < query.MinX || query.MaxY < query.MinY)
            throw new ArgumentException("Layer viewport bounds are invalid.", nameof(query));
        if (query.MinX is { } minX && query.MinY is { } minY && query.MaxX is { } maxX && query.MaxY is { } maxY &&
            checked((long)(maxX - minX + 1) * (maxY - minY + 1)) > MaximumCellsPerRequest)
            throw new ArgumentOutOfRangeException(nameof(query), $"Layer viewport cannot exceed {MaximumCellsPerRequest} cells.");
        return await repository.LoadAsync(query, cancellationToken)
            ?? throw new KeyNotFoundException($"World '{query.WorldId}' was not found.");
    }
}
