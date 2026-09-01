using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Worlds.Content;
using RpgWorld.Domain.Worlds.Content;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Infrastructure.Persistence;
using RpgWorld.Modules.Abstractions;
using RpgWorld.Modules.Abstractions.Definitions;

namespace RpgWorld.Infrastructure.Worlds.Content;

public sealed class CustomContentService(
    RpgWorldDbContext dbContext,
    IRpgContentCatalog moduleContent,
    TimeProvider timeProvider) : ICustomContentService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<CustomContentView>> ListAsync(
        Guid worldId, CancellationToken cancellationToken = default)
    {
        await RequireWorldAsync(worldId, cancellationToken);
        return await dbContext.CustomContentDefinitions.AsNoTracking()
            .Where(value => value.WorldId == worldId)
            .OrderBy(value => value.Kind).ThenBy(value => value.Code)
            .Select(value => View(value)).ToArrayAsync(cancellationToken);
    }

    public async Task<CustomContentView> CreateAsync(
        Guid worldId, CustomContentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await RequireWorldAsync(worldId, cancellationToken);
        Validate(request.Kind, request.Code, request.Name, request.Payload);
        var definition = CustomContentDefinition.Create(
            worldId, request.Kind, request.Code, request.Name, request.Payload, timeProvider.GetUtcNow());
        if (await dbContext.CustomContentDefinitions.AnyAsync(value => value.WorldId == worldId &&
                value.Kind == request.Kind && value.Code == definition.Code, cancellationToken))
            throw new InvalidOperationException($"Custom {request.Kind} definition '{definition.Code}' already exists.");
        dbContext.CustomContentDefinitions.Add(definition);
        await dbContext.SaveChangesAsync(cancellationToken);
        return View(definition);
    }

    public async Task<CustomContentView> UpdateAsync(
        Guid worldId, Guid definitionId, UpdateCustomContentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var definition = await RequiredAsync(worldId, definitionId, cancellationToken);
        Validate(definition.Kind, definition.Code, request.Name, request.Payload);
        definition.Update(request.Name, request.Payload, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return View(definition);
    }

    public async Task DeleteAsync(Guid worldId, Guid definitionId, CancellationToken cancellationToken = default)
    {
        dbContext.CustomContentDefinitions.Remove(await RequiredAsync(worldId, definitionId, cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<CustomContentExport> ExportAsync(Guid worldId, CancellationToken cancellationToken = default) =>
        new(1, worldId, timeProvider.GetUtcNow(), await ListAsync(worldId, cancellationToken));

    public async Task<IRpgContentCatalog> ResolveCatalogAsync(
        Guid worldId, CancellationToken cancellationToken = default)
    {
        var values = await dbContext.CustomContentDefinitions.AsNoTracking()
            .Where(value => value.WorldId == worldId).ToArrayAsync(cancellationToken);
        await RequireWorldAsync(worldId, cancellationToken);
        return new RpgContentOverlayCatalog(
            moduleContent,
            values.Where(value => value.Kind == CustomContentKind.Creature).Select(Creature),
            values.Where(value => value.Kind == CustomContentKind.Item).Select(Item),
            values.Where(value => value.Kind == CustomContentKind.Biome).Select(Biome),
            values.Where(value => value.Kind == CustomContentKind.Rule).Select(Rule));
    }

    private void Validate(CustomContentKind kind, string code, string name, string payload)
    {
        var candidate = CustomContentDefinition.Create(Guid.NewGuid(), kind, code, name, payload, timeProvider.GetUtcNow());
        switch (kind)
        {
            case CustomContentKind.Creature: _ = Creature(candidate); break;
            case CustomContentKind.Item: _ = Item(candidate); break;
            case CustomContentKind.Biome: _ = Biome(candidate); break;
            case CustomContentKind.Rule: _ = Rule(candidate); break;
        }
    }

    private CreatureDefinition Creature(CustomContentDefinition value)
    {
        var payload = Deserialize<CreaturePayload>(value);
        return new CreatureDefinition(value.Code, value.Name, payload.MaximumHealth, payload.Tags);
    }

    private ItemDefinition Item(CustomContentDefinition value)
    {
        var payload = Deserialize<ItemPayload>(value);
        return new ItemDefinition(value.Code, value.Name, payload.Category, payload.Stackable, payload.Tags);
    }

    private BiomeDefinition Biome(CustomContentDefinition value)
    {
        var payload = Deserialize<BiomePayload>(value);
        if (!moduleContent.TryResolveTerrain(payload.TerrainCode, out _))
            throw new ArgumentException($"Biome references unknown terrain '{payload.TerrainCode}'.");
        return new BiomeDefinition(value.Code, value.Name, payload.TerrainCode,
            payload.MinimumTemperatureCelsius, payload.MaximumTemperatureCelsius,
            payload.MinimumHumidity, payload.MaximumHumidity, payload.MovementCostMultiplier,
            payload.ResourceTags, payload.SpawnTags);
    }

    private static RuleDefinition Rule(CustomContentDefinition value)
    {
        var payload = Deserialize<RulePayload>(value);
        return new RuleDefinition(value.Code, value.Name,
            payload.Parameters ?? throw new ArgumentException("Rule parameters are required."));
    }

    private static T Deserialize<T>(CustomContentDefinition value) where T : class =>
        JsonSerializer.Deserialize<T>(value.Payload, JsonOptions)
        ?? throw new ArgumentException($"Payload for {value.Kind} is invalid.");

    private async Task RequireWorldAsync(Guid worldId, CancellationToken cancellationToken)
    {
        if (worldId == Guid.Empty || !await dbContext.Worlds.AsNoTracking()
                .AnyAsync(value => value.Id == worldId, cancellationToken))
            throw new KeyNotFoundException($"World '{worldId}' was not found.");
    }

    private async Task<CustomContentDefinition> RequiredAsync(
        Guid worldId, Guid definitionId, CancellationToken cancellationToken) =>
        await dbContext.CustomContentDefinitions.SingleOrDefaultAsync(value =>
            value.Id == definitionId && value.WorldId == worldId, cancellationToken)
        ?? throw new KeyNotFoundException($"Custom content definition '{definitionId}' was not found.");

    private static CustomContentView View(CustomContentDefinition value) =>
        new(value.Id, value.WorldId, value.Kind, value.Code, value.Name, value.Payload,
            value.Version, value.CreatedAtUtc, value.UpdatedAtUtc);

    private sealed record CreaturePayload(int MaximumHealth, string[]? Tags);
    private sealed record ItemPayload(string Category, bool Stackable, string[]? Tags);
    private sealed record BiomePayload(
        string TerrainCode,
        decimal MinimumTemperatureCelsius,
        decimal MaximumTemperatureCelsius,
        decimal MinimumHumidity,
        decimal MaximumHumidity,
        decimal MovementCostMultiplier = 1m,
        string[]? ResourceTags = null,
        string[]? SpawnTags = null);
    private sealed record RulePayload(IReadOnlyDictionary<string, decimal>? Parameters);
}
