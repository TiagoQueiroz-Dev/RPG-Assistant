using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Domain.Worlds.Resources;

namespace RpgWorld.Application.Worlds.Resources;

public sealed class NaturalResourceService(
    INaturalResourceRepository repository,
    IWorldDefinitionCatalog definitions) : INaturalResourceService
{
    public async Task<ResourceDepositSnapshot> SpawnOnTileAsync(
        Guid worldId,
        int x,
        int y,
        string resourceCode,
        DateTimeOffset worldInstant,
        ResourceSpawnOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var world = await RequiredWorldAsync(worldId, cancellationToken);
        var position = world.PositionAt(x, y);
        var tile = await repository.GetTileAsync(position, cancellationToken)
            ?? throw new KeyNotFoundException($"Tile ({x}, {y}) was not found.");
        var definition = definitions.ResolveResource(resourceCode);
        var locationTags = definitions.ResolveTerrain(tile.TerrainCode).ResourceTags
            .Concat(definitions.ResolveBiome(tile.BiomeCode).ResourceTags);
        if (!definition.Supports(locationTags))
            throw new InvalidOperationException($"Resource '{definition.Code}' is not compatible with tile ({x}, {y}).");
        options ??= new ResourceSpawnOptions();
        var deposit = ResourceDeposit.SpawnOnTile(
            world,
            tile,
            definition,
            worldInstant,
            options.InitialQuantity,
            options.Capacity,
            options.RegenerationPerWorldHour,
            options.SourceWorldEventId);
        repository.Add(deposit);
        await repository.SaveChangesAsync(cancellationToken);
        return ToSnapshot(deposit);
    }

    public async Task<ResourceDepositSnapshot> SpawnInRegionAsync(
        Guid worldId,
        ChunkCoordinate region,
        string resourceCode,
        DateTimeOffset worldInstant,
        ResourceSpawnOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var world = await RequiredWorldAsync(worldId, cancellationToken);
        var definition = definitions.ResolveResource(resourceCode);
        options ??= new ResourceSpawnOptions();
        var deposit = ResourceDeposit.SpawnInRegion(
            world,
            region,
            definition,
            worldInstant,
            options.InitialQuantity,
            options.Capacity,
            options.RegenerationPerWorldHour,
            options.SourceWorldEventId);
        repository.Add(deposit);
        await repository.SaveChangesAsync(cancellationToken);
        return ToSnapshot(deposit);
    }

    public async Task<bool> DiscoverAsync(
        Guid depositId,
        Guid actorId,
        DateTimeOffset worldInstant,
        CancellationToken cancellationToken = default)
    {
        var deposit = await RequiredDepositAsync(depositId, cancellationToken);
        var actor = await repository.GetActorAsync(actorId, cancellationToken)
            ?? throw new KeyNotFoundException($"Actor '{actorId}' was not found.");
        if (actor.WorldId != deposit.WorldId)
            throw new InvalidOperationException("Actor and resource deposit must belong to the same world.");
        if (actor.Status == ActorStatus.Dead)
            throw new InvalidOperationException("A dead actor cannot discover resources.");
        var discovered = deposit.Discover(actorId, worldInstant);
        if (discovered) await repository.SaveChangesAsync(cancellationToken);
        return discovered;
    }

    public async Task<ResourceExtraction> CollectForActorAsync(
        Guid depositId,
        Guid actorId,
        int requestedQuantity,
        DateTimeOffset worldInstant,
        CancellationToken cancellationToken = default)
    {
        if (requestedQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(requestedQuantity));
        var deposit = await RequiredDepositAsync(depositId, cancellationToken);
        var actor = await repository.GetActorAsync(actorId, cancellationToken)
            ?? throw new KeyNotFoundException($"Actor '{actorId}' was not found.");
        if (actor.WorldId != deposit.WorldId)
            throw new InvalidOperationException("Actor and resource deposit must belong to the same world.");
        if (actor.Status == ActorStatus.Dead)
            throw new InvalidOperationException("A dead actor cannot collect resources.");
        if (!deposit.IsDiscovered)
            throw new InvalidOperationException("Resource must be discovered before collection.");
        deposit.RegenerateTo(worldInstant);
        var collectable = (int)Math.Min(requestedQuantity, decimal.Floor(deposit.Quantity));
        if (collectable == 0) throw new InvalidOperationException("Resource deposit has no whole unit available.");
        var extraction = deposit.Extract(collectable, ResourceConsumer.Actor(actorId), worldInstant);
        actor.AddInventory(deposit.InventoryItemCode, collectable, worldInstant);
        await repository.SaveChangesAsync(cancellationToken);
        return extraction;
    }

    public async Task<ResourceExtraction> ConsumeAsync(
        Guid depositId,
        ResourceConsumer consumer,
        decimal requestedQuantity,
        DateTimeOffset worldInstant,
        CancellationToken cancellationToken = default)
    {
        var deposit = await RequiredDepositAsync(depositId, cancellationToken);
        var extraction = deposit.Extract(requestedQuantity, consumer, worldInstant);
        await repository.SaveChangesAsync(cancellationToken);
        return extraction;
    }

    public async Task<IReadOnlyList<ResourceDepositSnapshot>> ListAvailableInRegionAsync(
        Guid worldId,
        Position origin,
        DateTimeOffset worldInstant,
        IReadOnlyCollection<string>? resourceCodes = null,
        CancellationToken cancellationToken = default)
    {
        var world = await RequiredWorldAsync(worldId, cancellationToken);
        if (!world.Contains(origin)) throw new ArgumentOutOfRangeException(nameof(origin));
        var deposits = await repository.ListAvailableInRegionAsync(
            worldId,
            world.ChunkAt(origin),
            resourceCodes?.Select(code => definitions.ResolveResource(code).Code).ToArray(),
            cancellationToken);
        var changed = false;
        foreach (var deposit in deposits)
            changed |= deposit.RegenerateTo(worldInstant) > 0m;
        if (changed) await repository.SaveChangesAsync(cancellationToken);
        return deposits.Where(deposit => !deposit.IsExhausted).Select(ToSnapshot).ToArray();
    }

    private async Task<World> RequiredWorldAsync(Guid worldId, CancellationToken cancellationToken) =>
        await repository.GetWorldAsync(worldId, cancellationToken)
        ?? throw new KeyNotFoundException($"World '{worldId}' was not found.");

    private async Task<ResourceDeposit> RequiredDepositAsync(Guid depositId, CancellationToken cancellationToken) =>
        await repository.GetDepositAsync(depositId, cancellationToken)
        ?? throw new KeyNotFoundException($"Resource deposit '{depositId}' was not found.");

    private static ResourceDepositSnapshot ToSnapshot(ResourceDeposit deposit) => new(
        deposit.Id,
        deposit.WorldId,
        deposit.ResourceCode,
        deposit.InventoryItemCode,
        deposit.Scope,
        deposit.TileId,
        deposit.RegionX,
        deposit.RegionY,
        deposit.Quantity,
        deposit.Capacity,
        deposit.RegenerationPerWorldHour,
        deposit.IsDiscovered,
        deposit.IsExhausted,
        deposit.SourceWorldEventId);
}
