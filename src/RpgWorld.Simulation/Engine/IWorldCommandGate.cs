namespace RpgWorld.Simulation.Engine;

public interface IWorldCommandGate
{
    Task ExecuteAsync(
        Guid worldId,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default);

    Task<T> ExecuteAsync<T>(
        Guid worldId,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default);
}
