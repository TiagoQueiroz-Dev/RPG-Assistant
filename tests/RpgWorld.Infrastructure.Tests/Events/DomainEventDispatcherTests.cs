using Microsoft.Extensions.DependencyInjection;
using RpgWorld.Application.Events;
using RpgWorld.Domain.Events;
using RpgWorld.Infrastructure.Events;
using RpgWorld.Domain.Worlds.Events;

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

    [Fact]
    public async Task Consequence_events_inherit_correlation_and_identify_their_immediate_cause()
    {
        var consumer = new CausalConsumer();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<ActorKilledEvent>>(consumer);
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var root = new ActorKilledEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

        await scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>().DispatchAsync([root]);

        var consequence = Assert.IsType<WorldConsequenceAppliedEvent>(consumer.Generated);
        Assert.Equal(root.EventId, consequence.CorrelationId);
        Assert.Equal(root.EventId, consequence.CausationId);
        Assert.Equal(1, consequence.CausalityDepth);
    }

    [Fact]
    public async Task Dispatcher_rejects_a_chain_beyond_the_depth_limit()
    {
        DomainEvent current = new TestEvent();
        for (var index = 0; index <= DomainEventDispatcher.MaximumCausalityDepth; index++)
        {
            using var scope = DomainEventCausality.Push(current);
            current = new TestEvent();
        }
        var services = new ServiceCollection();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        await using var provider = services.BuildServiceProvider();
        await using var serviceScope = provider.CreateAsyncScope();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            serviceScope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>().DispatchAsync([current]));
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

    private sealed class CausalConsumer : IDomainEventHandler<ActorKilledEvent>
    {
        public IDomainEvent? Generated { get; private set; }
        public Task HandleAsync(ActorKilledEvent domainEvent, CancellationToken cancellationToken = default)
        {
            Generated = new WorldConsequenceAppliedEvent(
                Guid.NewGuid(), domainEvent.WorldId, WorldConsequenceKind.Crime, domainEvent.ActorId,
                10m, "Generated consequence.", domainEvent.EventId, domainEvent.OccurredAtUtc);
            return Task.CompletedTask;
        }
    }

    private sealed record TestEvent() : DomainEvent(DateTimeOffset.UnixEpoch);
}
