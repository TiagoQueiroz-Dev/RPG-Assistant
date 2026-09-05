namespace RpgWorld.Application.Events;

public interface IWorldEffectQueue
{
    Task RunAfterCommitAsync(Func<CancellationToken, Task> effect, CancellationToken cancellationToken = default);
}
