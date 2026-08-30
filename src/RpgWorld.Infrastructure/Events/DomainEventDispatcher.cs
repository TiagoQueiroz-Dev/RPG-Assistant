using Microsoft.Extensions.DependencyInjection;
using RpgWorld.Application.Events;
using RpgWorld.Domain.Events;

namespace RpgWorld.Infrastructure.Events;

public sealed class DomainEventDispatcher(IServiceProvider serviceProvider)
    : IDomainEventDispatcher
{
    public async Task DispatchAsync(
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        foreach (var domainEvent in domainEvents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var serviceType = typeof(IDomainEventHandler<>)
                .MakeGenericType(domainEvent.GetType());
            var handlers = serviceProvider.GetServices(serviceType)
                .Cast<IDomainEventHandler>()
                .ToArray();

            await Task.WhenAll(handlers.Select(handler =>
                handler.HandleAsync(domainEvent, cancellationToken)));
        }
    }
}
