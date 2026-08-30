using RpgWorld.Domain.Events;

namespace RpgWorld.Application.Events;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default);
}

