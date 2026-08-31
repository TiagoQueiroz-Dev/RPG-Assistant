using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Definitions;

namespace RpgWorld.Simulation.Actors;

public sealed class AdjacentTileMovementPolicy : IActorMovementPolicy
{
    private const decimal DiagonalMultiplier = 1.41421356237m;

    public ActorMovementEvaluation Evaluate(
        Actor actor,
        Tile origin,
        Tile destination,
        IWorldDefinitionCatalog definitions)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(definitions);
        if (origin.WorldId != actor.WorldId || destination.WorldId != actor.WorldId)
            throw new InvalidOperationException("Movement tiles must belong to the actor's world.");
        var deltaX = Math.Abs(destination.X - origin.X);
        var deltaY = Math.Abs(destination.Y - origin.Y);
        if ((deltaX == 0 && deltaY == 0) || deltaX > 1 || deltaY > 1)
            throw new InvalidOperationException("Basic movement must target an adjacent tile; use pathfinding for longer routes.");
        var terrain = definitions.ResolveTerrain(destination.TerrainCode);
        if (!terrain.IsTraversable)
            throw new InvalidOperationException($"Terrain '{terrain.Code}' blocks movement.");
        var biome = definitions.ResolveBiome(destination.BiomeCode);
        var diagonal = deltaX == 1 && deltaY == 1 ? DiagonalMultiplier : 1m;
        return new ActorMovementEvaluation(
            decimal.Round(terrain.MovementCost * biome.MovementCostMultiplier * diagonal, 4));
    }
}
