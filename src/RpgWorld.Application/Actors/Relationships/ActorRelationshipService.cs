using RpgWorld.Domain.Actors;

namespace RpgWorld.Application.Actors.Relationships;

public sealed class ActorRelationshipService(IActorRepository repository) : IActorRelationshipService
{
    public async Task<ActorRelationship> ApplyAsync(
        ActorRelationshipChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SourceActorId == request.TargetActorId)
            throw new ArgumentException("An actor cannot have a directed relationship with itself.", nameof(request));
        var source = await repository.GetAsync(request.SourceActorId, cancellationToken)
            ?? throw new KeyNotFoundException($"Actor '{request.SourceActorId}' was not found.");
        var target = await repository.GetAsync(request.TargetActorId, cancellationToken)
            ?? throw new KeyNotFoundException($"Actor '{request.TargetActorId}' was not found.");
        if (source.WorldId != target.WorldId)
            throw new InvalidOperationException("Related actors must belong to the same world.");
        source.ApplyRelationship(target.Id, request.Modifier, request.OccurredAt);
        await repository.SaveChangesAsync(cancellationToken);
        return source.Relationships.Single(relationship => relationship.ActorId == target.Id);
    }
}
