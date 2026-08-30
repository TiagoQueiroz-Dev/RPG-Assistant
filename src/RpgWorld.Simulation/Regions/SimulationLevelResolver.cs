using RpgWorld.Domain.Worlds;

namespace RpgWorld.Simulation.Regions;

public sealed class SimulationLevelResolver(SimulationLevelOptions options)
{
    public SimulationLevel Resolve(
        World world,
        ChunkCoordinate region,
        IEnumerable<Position> playerPositions,
        bool hasRelevantActivity = false)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(playerPositions);
        if (hasRelevantActivity) return SimulationLevel.Detailed;

        var distances = playerPositions.Select(position =>
        {
            var playerRegion = world.ChunkAt(position);
            return Math.Max(
                Math.Abs(playerRegion.X - region.X),
                Math.Abs(playerRegion.Y - region.Y));
        }).ToArray();
        if (distances.Length == 0) return SimulationLevel.Abstract;
        var distance = distances.Min();
        if (distance <= options.DetailedRadius) return SimulationLevel.Detailed;
        return distance <= options.RegionalRadius
            ? SimulationLevel.Regional
            : SimulationLevel.Abstract;
    }
}
