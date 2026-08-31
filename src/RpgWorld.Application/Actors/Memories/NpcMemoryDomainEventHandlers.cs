using RpgWorld.Application.Events;
using RpgWorld.Domain.Events;

namespace RpgWorld.Application.Actors.Memories;

public sealed class NpcDamagedMemoryHandler(NpcMemoryEventRecorder recorder)
    : IDomainEventHandler<ActorDamagedEvent>
{
    public Task HandleAsync(ActorDamagedEvent domainEvent, CancellationToken cancellationToken = default) =>
        recorder.RecordAsync(domainEvent, cancellationToken);
}

public sealed class NpcFamilyKilledMemoryHandler(NpcMemoryEventRecorder recorder)
    : IDomainEventHandler<ActorKilledEvent>
{
    public Task HandleAsync(ActorKilledEvent domainEvent, CancellationToken cancellationToken = default) =>
        recorder.RecordAsync(domainEvent, cancellationToken);
}
