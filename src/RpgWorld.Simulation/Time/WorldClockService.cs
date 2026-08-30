using RpgWorld.Application.Worlds;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Simulation.Time;

public sealed class WorldClockService(
    IWorldClockRepository repository,
    TimeProvider timeProvider) : IWorldClockService
{
    public async Task<WorldClockSnapshot> GetAsync(
        Guid worldId,
        CancellationToken cancellationToken = default) =>
        Snapshot(await GetOrCreateAsync(worldId, cancellationToken));

    public async Task<WorldClockSnapshot> AdvanceTicksAsync(
        Guid worldId,
        int tickCount = 1,
        CancellationToken cancellationToken = default)
    {
        var clock = await GetOrCreateAsync(worldId, cancellationToken);
        clock.AdvanceTicks(tickCount);
        await repository.SaveChangesAsync(cancellationToken);
        return Snapshot(clock);
    }

    public async Task<WorldClockSnapshot> AdvanceByAsync(
        Guid worldId,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        var clock = await GetOrCreateAsync(worldId, cancellationToken);
        clock.AdvanceBy(duration);
        await repository.SaveChangesAsync(cancellationToken);
        return Snapshot(clock);
    }

    public async Task<WorldClockSnapshot> SynchronizeAsync(
        Guid worldId,
        CancellationToken cancellationToken = default)
    {
        var clock = await GetOrCreateAsync(worldId, cancellationToken);
        clock.Synchronize(timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return Snapshot(clock);
    }

    public async Task<WorldClockSnapshot> RebaseAsync(
        Guid worldId,
        CancellationToken cancellationToken = default)
    {
        var clock = await GetOrCreateAsync(worldId, cancellationToken);
        clock.Rebase(timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return Snapshot(clock);
    }

    public async Task<WorldClockSnapshot> ConfigureAsync(
        Guid worldId,
        TimeSpan tickDuration,
        decimal realTimeMultiplier,
        CancellationToken cancellationToken = default)
    {
        var clock = await GetOrCreateAsync(worldId, cancellationToken);
        clock.SetTickDuration(tickDuration);
        clock.SetRealTimeMultiplier(realTimeMultiplier);
        await repository.SaveChangesAsync(cancellationToken);
        return Snapshot(clock);
    }

    private async Task<WorldClock> GetOrCreateAsync(
        Guid worldId,
        CancellationToken cancellationToken)
    {
        var clock = await repository.GetAsync(worldId, cancellationToken);
        if (clock is not null) return clock;

        if (!await repository.WorldExistsAsync(worldId, cancellationToken))
        {
            throw new KeyNotFoundException("World was not found.");
        }

        var now = timeProvider.GetUtcNow();
        clock = WorldClock.Create(worldId, now, now);
        repository.Add(clock);
        await repository.SaveChangesAsync(cancellationToken);
        return clock;
    }

    private static WorldClockSnapshot Snapshot(WorldClock clock) => new(
        clock.WorldId,
        clock.CurrentInstant,
        clock.TickDuration,
        clock.RealTimeMultiplier,
        clock.LastSynchronizedAtUtc);
}
