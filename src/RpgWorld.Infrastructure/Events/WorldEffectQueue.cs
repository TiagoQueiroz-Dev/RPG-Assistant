using Microsoft.Extensions.Logging;
using RpgWorld.Application.Events;

namespace RpgWorld.Infrastructure.Events;

public sealed class WorldEffectQueue(ILogger<WorldEffectQueue>? logger = null) : IWorldEffectQueue
{
    private List<Func<CancellationToken, Task>>? _pending;

    public void Begin()
    {
        if (_pending is not null) throw new InvalidOperationException("An effect batch is already active.");
        _pending = [];
    }

    public Task RunAfterCommitAsync(Func<CancellationToken, Task> effect, CancellationToken cancellationToken = default)
    {
        if (_pending is null) return effect(cancellationToken);
        _pending.Add(effect);
        return Task.CompletedTask;
    }

    public void Discard() => _pending = null;

    public async Task FlushAsync()
    {
        var effects = _pending ?? [];
        _pending = null;
        foreach (var effect in effects)
        {
            try { await effect(CancellationToken.None); }
            catch (Exception exception)
            {
                // The database is committed. Delivery failure must not replay world effects.
                logger?.LogError(exception, "Post-commit world effect delivery failed; client resynchronization may be required.");
            }
        }
    }
}
