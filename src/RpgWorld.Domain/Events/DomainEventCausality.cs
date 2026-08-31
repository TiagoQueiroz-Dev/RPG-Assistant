namespace RpgWorld.Domain.Events;

public static class DomainEventCausality
{
    private static readonly AsyncLocal<IDomainEvent?> CurrentEvent = new();

    public static IDomainEvent? Current => CurrentEvent.Value;

    public static IDisposable Push(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var previous = CurrentEvent.Value;
        CurrentEvent.Value = domainEvent;
        return new Scope(previous);
    }

    private sealed class Scope(IDomainEvent? previous) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            CurrentEvent.Value = previous;
            _disposed = true;
        }
    }
}
