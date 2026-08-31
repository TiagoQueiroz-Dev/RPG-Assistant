using RpgWorld.Domain.Actors;

namespace RpgWorld.Application.Actors.Relationships;

public interface IActorRelationshipService
{
    Task<ActorRelationship> ApplyAsync(ActorRelationshipChangeRequest request, CancellationToken cancellationToken = default);
}

public sealed record ActorRelationshipChangeRequest(
    Guid SourceActorId,
    Guid TargetActorId,
    ActorRelationshipModifier Modifier,
    DateTimeOffset OccurredAt);
