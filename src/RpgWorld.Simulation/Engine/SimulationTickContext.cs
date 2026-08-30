using RpgWorld.Simulation.Time;

namespace RpgWorld.Simulation.Engine;

public sealed record SimulationTickContext(
    Guid WorldId,
    WorldClockSnapshot Clock);
