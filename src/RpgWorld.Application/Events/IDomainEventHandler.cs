using RpgWorld.Domain.Events;

namespace RpgWorld.Application.Events;

public interface IDomainEventHandler
{
    Type EventType { get; }

    Task HandleAsync(
        IDomainEvent domainEvent,
        CancellationToken cancellationToken = default);
}

public interface IDomainEventHandler<in TEvent> : IDomainEventHandler
    where TEvent : IDomainEvent
{
    Type IDomainEventHandler.EventType => typeof(TEvent);

    Task HandleAsync(
        TEvent domainEvent,
        CancellationToken cancellationToken = default);

    Task IDomainEventHandler.HandleAsync(
        IDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        HandleAsync((TEvent)domainEvent, cancellationToken);
}

