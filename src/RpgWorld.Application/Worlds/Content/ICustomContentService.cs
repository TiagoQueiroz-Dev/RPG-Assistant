using RpgWorld.Domain.Worlds.Content;
using RpgWorld.Modules.Abstractions;

namespace RpgWorld.Application.Worlds.Content;

public sealed record CustomContentRequest(
    CustomContentKind Kind,
    string Code,
    string Name,
    string Payload);

public sealed record UpdateCustomContentRequest(string Name, string Payload);

public sealed record CustomContentView(
    Guid Id,
    Guid WorldId,
    CustomContentKind Kind,
    string Code,
    string Name,
    string Payload,
    int Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record CustomContentExport(
    int SchemaVersion,
    Guid WorldId,
    DateTimeOffset ExportedAtUtc,
    IReadOnlyList<CustomContentView> Definitions);

public interface ICampaignContentCatalogProvider
{
    Task<IRpgContentCatalog> ResolveCatalogAsync(Guid worldId, CancellationToken cancellationToken = default);
}

public interface ICustomContentService : ICampaignContentCatalogProvider
{
    Task<IReadOnlyList<CustomContentView>> ListAsync(Guid worldId, CancellationToken cancellationToken = default);
    Task<CustomContentView> CreateAsync(Guid worldId, CustomContentRequest request, CancellationToken cancellationToken = default);
    Task<CustomContentView> UpdateAsync(Guid worldId, Guid definitionId, UpdateCustomContentRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid worldId, Guid definitionId, CancellationToken cancellationToken = default);
    Task<CustomContentExport> ExportAsync(Guid worldId, CancellationToken cancellationToken = default);
}
