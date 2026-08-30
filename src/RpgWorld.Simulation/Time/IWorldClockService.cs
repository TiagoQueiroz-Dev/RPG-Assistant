namespace RpgWorld.Simulation.Time;

public interface IWorldClockService
{
    Task<WorldClockSnapshot> GetAsync(Guid worldId, CancellationToken cancellationToken = default);

    Task<WorldClockSnapshot> AdvanceTicksAsync(
        Guid worldId,
        int tickCount = 1,
        CancellationToken cancellationToken = default);

    Task<WorldClockSnapshot> SynchronizeAsync(Guid worldId, CancellationToken cancellationToken = default);

    Task<WorldClockSnapshot> ConfigureAsync(
        Guid worldId,
        TimeSpan tickDuration,
        decimal realTimeMultiplier,
        CancellationToken cancellationToken = default);
}
