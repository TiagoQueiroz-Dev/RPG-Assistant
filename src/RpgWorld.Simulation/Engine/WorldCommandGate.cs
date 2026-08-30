using System.Collections.Concurrent;

namespace RpgWorld.Simulation.Engine;

public sealed class WorldCommandGate : IWorldCommandGate, IDisposable
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _gates = [];

    public Task ExecuteAsync(
        Guid worldId,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<object?>(worldId, async token =>
        {
            await action(token);
            return null;
        }, cancellationToken);

    public async Task<T> ExecuteAsync<T>(
        Guid worldId,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        if (worldId == Guid.Empty) throw new ArgumentException("World identifier is required.", nameof(worldId));
        ArgumentNullException.ThrowIfNull(action);
        var gate = _gates.GetOrAdd(worldId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try { return await action(cancellationToken); }
        finally { gate.Release(); }
    }

    public void Dispose()
    {
        foreach (var gate in _gates.Values) gate.Dispose();
    }
}
