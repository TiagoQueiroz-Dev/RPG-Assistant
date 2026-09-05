using RpgWorld.Domain.Actors.Actions;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Domain.Events;

public sealed record NpcActionExecutionChangedEvent(Guid ActorId, Guid WorldId, Position Position,
    NpcActionExecution Execution, bool IsStarting) : DomainEvent(Execution.UpdatedAt);
