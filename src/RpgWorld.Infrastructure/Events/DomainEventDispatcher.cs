using Microsoft.Extensions.DependencyInjection;
using RpgWorld.Application.Events;
using RpgWorld.Domain.Events;

namespace RpgWorld.Infrastructure.Events;

public sealed class DomainEventDispatcher(IServiceProvider serviceProvider)
    : IDomainEventDispatcher
{
    public const int MaximumCausalityDepth = 16;

    public async Task DispatchAsync(
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        foreach (var domainEvent in domainEvents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (domainEvent.CausalityDepth > MaximumCausalityDepth)
                throw new InvalidOperationException(
                    $"Domain event causality depth exceeded {MaximumCausalityDepth} levels.");

            var serviceType = typeof(IDomainEventHandler<>)
                .MakeGenericType(domainEvent.GetType());
            var handlers = serviceProvider.GetServices(serviceType)
                .Cast<IDomainEventHandler>()
                .ToArray();

            using var scope = DomainEventCausality.Push(domainEvent);
            foreach (var handler in handlers)
                await handler.HandleAsync(domainEvent, cancellationToken);
        }
    }
}
