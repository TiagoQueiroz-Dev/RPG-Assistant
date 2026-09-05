using RpgWorld.Application.Actors.Movement;
using RpgWorld.Application.Worlds.Content;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Definitions;

namespace RpgWorld.Simulation.Actors;

public sealed class AStarActorPathfinder(IPathfindingMapStore store, IWorldDefinitionCatalog definitions,
    IActorMovementPolicy movementPolicy, ICampaignContentCatalogProvider? campaignContent = null) : IActorPathfinder
{
    private static readonly (int X, int Y)[] Directions =
        [(-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1)];

    public async Task<ActorPathResult> FindAsync(Actor actor, Position destination, PathfindingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        options ??= new PathfindingOptions();
        options.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        var world = await store.GetWorldAsync(actor.WorldId, cancellationToken)
            ?? throw new KeyNotFoundException("Actor world was not found.");
        if (!world.Contains(actor.Position) || !world.Contains(destination))
            throw new ArgumentException("Path endpoints must be inside the actor's world.", nameof(destination));
        if (actor.Status == ActorStatus.Dead) return Missing("Dead actors cannot navigate.");
        if (actor.Position == destination) return new(ActorPathStatus.Found, [], 0m, 0);

        var bounds = new NavigationBounds(
            Math.Max(0, Math.Min(actor.X, destination.X) - options.SearchPadding),
            Math.Max(0, Math.Min(actor.Y, destination.Y) - options.SearchPadding),
            (int)Math.Min(world.Width - 1L, Math.Max(actor.X, destination.X) + (long)options.SearchPadding),
            (int)Math.Min(world.Height - 1L, Math.Max(actor.Y, destination.Y) + (long)options.SearchPadding));
        var area = (bounds.MaxX - (long)bounds.MinX + 1) * (bounds.MaxY - (long)bounds.MinY + 1);
        if (area > options.MaximumLoadedTiles) return Limited(0, "Tile load budget exceeded; narrow the search area.");
        var tiles = (await store.GetTilesAsync(world.Id, bounds, options.MaximumLoadedTiles, cancellationToken))
            .ToDictionary(tile => (tile.X, tile.Y));
        var catalog = campaignContent is null ? definitions : await campaignContent.ResolveCatalogAsync(world.Id, cancellationToken);
        var source = (actor.X, actor.Y);
        var goal = (destination.X, destination.Y);
        if (!tiles.ContainsKey(source) || !tiles.TryGetValue(goal, out var goalTile)) return Missing("An endpoint tile is missing.");
        if (!catalog.ResolveTerrain(goalTile.TerrainCode).IsTraversable) return Missing("Destination terrain is blocked.");

        // Chebyshev distance times the cheapest rounded step is admissible for the eight-way movement policy.
        // Other policies may define lower costs, so use zero (Dijkstra) for those policies.
        var minimumCost = movementPolicy is AdjacentTileMovementPolicy
            ? tiles.Values.Where(tile => catalog.ResolveTerrain(tile.TerrainCode).IsTraversable)
                .Select(tile => decimal.Round(catalog.ResolveTerrain(tile.TerrainCode).MovementCost *
                    catalog.ResolveBiome(tile.BiomeCode).MovementCostMultiplier, 4)).DefaultIfEmpty(0m).Min()
            : 0m;
        decimal Heuristic((int X, int Y) point) =>
            Math.Max(Math.Abs(point.X - goal.X), Math.Abs(point.Y - goal.Y)) * minimumCost;
        var frontier = new PriorityQueue<((int X, int Y) Point, decimal Cost), (decimal Estimate, decimal Remaining, int Y, int X)>();
        var costs = new Dictionary<(int X, int Y), decimal> { [source] = 0m };
        var parents = new Dictionary<(int X, int Y), (int X, int Y)>();
        var closed = new HashSet<(int X, int Y)>();
        frontier.Enqueue((source, 0m), (Heuristic(source), Heuristic(source), source.Y, source.X));
        var expanded = 0;
        while (frontier.TryDequeue(out var entry, out _))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = entry.Point;
            if (entry.Cost != costs[current] || closed.Contains(current)) continue;
            if (current == goal)
            {
                var route = new List<Position>();
                while (current != source)
                {
                    route.Add(new Position(world.Id, current.X, current.Y));
                    current = parents[current];
                }
                route.Reverse();
                return new(ActorPathStatus.Found, route, entry.Cost, expanded);
            }
            if (expanded >= options.MaximumExpandedNodes) return Limited(expanded, "Expanded node budget exceeded.");
            closed.Add(current);
            expanded++;
            foreach (var direction in Directions)
            {
                var next = (X: current.X + direction.X, Y: current.Y + direction.Y);
                if (closed.Contains(next) || !tiles.TryGetValue(next, out var tile)) continue;
                if (!catalog.ResolveTerrain(tile.TerrainCode).IsTraversable) continue;
                decimal stepCost;
                try { stepCost = movementPolicy.Evaluate(actor, tiles[current], tile, catalog).MovementCost; }
                catch (InvalidOperationException) { continue; }
                if (stepCost < 0m) throw new InvalidOperationException("Pathfinding requires nonnegative movement costs.");
                var cost = entry.Cost + stepCost;
                if (costs.TryGetValue(next, out var previous) && cost >= previous) continue;
                costs[next] = cost;
                parents[next] = current;
                var remaining = Heuristic(next);
                frontier.Enqueue((next, cost), (cost + remaining, remaining, next.Y, next.X));
            }
        }
        var coversWorld = bounds.MinX == 0 && bounds.MinY == 0 && bounds.MaxX == world.Width - 1 && bounds.MaxY == world.Height - 1;
        return coversWorld ? Missing("Destination is unreachable.", expanded)
            : Limited(expanded, "No route within the search bounds; widen the search before declaring it unreachable.");
    }

    private static ActorPathResult Missing(string reason, int nodes = 0) => new(ActorPathStatus.NoPath, [], 0m, nodes, reason);
    private static ActorPathResult Limited(int nodes, string reason) => new(ActorPathStatus.SearchLimitReached, [], 0m, nodes, reason);
}
