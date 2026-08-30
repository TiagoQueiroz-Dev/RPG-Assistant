namespace RpgWorld.Domain.Worlds;

public sealed class Chunk
{
    private Chunk()
    {
    }

    private Chunk(
        Guid id,
        Guid worldId,
        ChunkCoordinate coordinate,
        int originX,
        int originY,
        int width,
        int height)
    {
        Id = id;
        WorldId = worldId;
        CoordinateX = coordinate.X;
        CoordinateY = coordinate.Y;
        OriginX = originX;
        OriginY = originY;
        Width = width;
        Height = height;
        SimulationLevel = SimulationLevel.Abstract;
    }

    public Guid Id { get; private set; }

    public Guid WorldId { get; private set; }

    public int CoordinateX { get; private set; }

    public int CoordinateY { get; private set; }

    public int OriginX { get; private set; }

    public int OriginY { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public SimulationLevel SimulationLevel { get; private set; }

    public int AggregatePopulation { get; private set; }

    public decimal AggregateEconomicOutput { get; private set; }

    public decimal AggregateMilitaryStrength { get; private set; }

    public decimal AggregateProductionOutput { get; private set; }

    public ChunkCoordinate Coordinate => new(CoordinateX, CoordinateY);

    public bool Contains(Position position) =>
        position.WorldId == WorldId &&
        position.X >= OriginX &&
        position.X < OriginX + Width &&
        position.Y >= OriginY &&
        position.Y < OriginY + Height;

    public bool AllowsIndividualActions => SimulationLevel == SimulationLevel.Detailed;

    public void TransitionSimulationLevel(
        SimulationLevel targetLevel,
        RegionAggregateState aggregateState)
    {
        ArgumentNullException.ThrowIfNull(aggregateState);
        SimulationLevel = targetLevel;
        AggregatePopulation = aggregateState.Population;
        AggregateEconomicOutput = aggregateState.EconomicOutput;
        AggregateMilitaryStrength = aggregateState.MilitaryStrength;
        AggregateProductionOutput = aggregateState.ProductionOutput;
    }

    public RegionAggregateState GetAggregateState() => new(
        AggregatePopulation,
        AggregateEconomicOutput,
        AggregateMilitaryStrength,
        AggregateProductionOutput);

    internal static Chunk Create(
        Guid worldId,
        ChunkCoordinate coordinate,
        int originX,
        int originY,
        int width,
        int height) =>
        new(
            Guid.CreateVersion7(),
            worldId,
            coordinate,
            originX,
            originY,
            width,
            height);
}
