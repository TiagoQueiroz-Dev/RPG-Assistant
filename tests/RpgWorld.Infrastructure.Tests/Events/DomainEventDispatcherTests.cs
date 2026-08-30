using Microsoft.Extensions.DependencyInjection;
using RpgWorld.Application.Events;
using RpgWorld.Domain.Events;
using RpgWorld.Infrastructure.Events;

namespace RpgWorld.Infrastructure.Tests.Events;

public sealed class DomainEventDispatcherTests
{
    [Fact]
    public async Task One_event_is_delivered_to_two_decoupled_consumers()
    {
        var reputationConsumer = new ReputationConsumer();
        var successionConsumer = new SuccessionConsumer();
        var services = new ServiceCollection();

        services.AddSingleton<IDomainEventHandler<ActorKilledEvent>>(reputationConsumer);
        services.AddSingleton<IDomainEventHandler<ActorKilledEvent>>(successionConsumer);
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
        var domainEvent = new ActorKilledEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        await dispatcher.DispatchAsync([domainEvent]);

        Assert.Equal(domainEvent.EventId, reputationConsumer.LastEventId);
        Assert.Equal(domainEvent.EventId, successionConsumer.LastEventId);
        Assert.Equal(1, reputationConsumer.Calls);
        Assert.Equal(1, successionConsumer.Calls);
    }

    private sealed class ReputationConsumer : IDomainEventHandler<ActorKilledEvent>
    {
        public int Calls { get; private set; }

        public Guid? LastEventId { get; private set; }

        public Task HandleAsync(
            ActorKilledEvent domainEvent,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastEventId = domainEvent.EventId;
            return Task.CompletedTask;
        }
    }

    private sealed class SuccessionConsumer : IDomainEventHandler<ActorKilledEvent>
    {
        public int Calls { get; private set; }

        public Guid? LastEventId { get; private set; }

        public Task HandleAsync(
            ActorKilledEvent domainEvent,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastEventId = domainEvent.EventId;
            return Task.CompletedTask;
        }
    }
}
