namespace RpgWorld.Application.Worlds.Admin;

public enum WorldMapLayerMode
{
    Normal, Political, Population, Economy, Resources, Military,
    Religion, Danger, Biome, Temperature, Faction
}

public sealed record WorldMapLayerQuery(
    Guid WorldId,
    WorldMapLayerMode Mode,
    int? MinX = null,
    int? MinY = null,
    int? MaxX = null,
    int? MaxY = null);

public sealed record WorldMapLayerCell(
    int X,
    int Y,
    decimal Intensity,
    string Label,
    string Color,
    Guid? EntityId = null);

public sealed record WorldMapLayerLegendItem(
    string Label,
    string Color,
    decimal? Minimum = null,
    decimal? Maximum = null);

public sealed record WorldMapLayerView(
    Guid WorldId,
    string Mode,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<WorldMapLayerCell> Cells,
    IReadOnlyList<WorldMapLayerLegendItem> Legend);

public interface IWorldMapLayerRepository
{
    Task<WorldMapLayerView?> LoadAsync(WorldMapLayerQuery query, CancellationToken cancellationToken = default);
}

public interface IWorldMapLayerService
{
    Task<WorldMapLayerView> LoadAsync(WorldMapLayerQuery query, CancellationToken cancellationToken = default);
}
