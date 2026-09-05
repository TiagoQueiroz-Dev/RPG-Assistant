using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Application.Actors.Movement;

public enum ActorPathStatus { Found, NoPath, SearchLimitReached }
public sealed record ActorPathResult(ActorPathStatus Status, IReadOnlyList<Position> Steps,
    decimal TotalCost, int ExpandedNodes, string? Reason = null);

public sealed record PathfindingOptions(int MaximumExpandedNodes = 10_000, int MaximumLoadedTiles = 65_536,
    int SearchPadding = 16)
{
    public void Validate()
    {
        if (MaximumExpandedNodes is < 1 or > 1_000_000 || MaximumLoadedTiles is < 1 or > 1_000_000 ||
            SearchPadding is < 0 or > 100_000) throw new ArgumentOutOfRangeException(nameof(PathfindingOptions));
    }
}

public interface IActorPathfinder
{
    Task<ActorPathResult> FindAsync(Actor actor, Position destination, PathfindingOptions? options = null,
        CancellationToken cancellationToken = default);
}

public readonly record struct NavigationBounds(int MinX, int MinY, int MaxX, int MaxY);

public interface IPathfindingMapStore
{
    Task<World?> GetWorldAsync(Guid worldId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tile>> GetTilesAsync(Guid worldId, NavigationBounds bounds, int limit,
        CancellationToken cancellationToken = default);
}
