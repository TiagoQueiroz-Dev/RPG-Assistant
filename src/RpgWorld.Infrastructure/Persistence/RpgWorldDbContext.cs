using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Events;
using RpgWorld.Domain;
using RpgWorld.Domain.Events;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Actors.Memories;
using RpgWorld.Domain.Actors.Housing;
using RpgWorld.Domain.Worlds.Resources;
using RpgWorld.Domain.Worlds.Cities;
using RpgWorld.Domain.Worlds.Factions;
using RpgWorld.Domain.Worlds.Events;
using RpgWorld.Domain.Worlds.Content;
using RpgWorld.Infrastructure.Events;

namespace RpgWorld.Infrastructure.Persistence;

public sealed class RpgWorldDbContext : DbContext
{
    private readonly IDomainEventDispatcher? _domainEventDispatcher;

    public RpgWorldDbContext(
        DbContextOptions<RpgWorldDbContext> options,
        IDomainEventDispatcher? domainEventDispatcher = null)
        : base(options)
    {
        _domainEventDispatcher = domainEventDispatcher;
    }

    public const string DefaultSchema = "rpg_world";

    public DbSet<PersistenceCheckpoint> PersistenceCheckpoints =>
        Set<PersistenceCheckpoint>();

    public DbSet<World> Worlds => Set<World>();

    public DbSet<Chunk> Chunks => Set<Chunk>();

    public DbSet<Tile> Tiles => Set<Tile>();

    public DbSet<WorldMapSourceImage> WorldMapSourceImages => Set<WorldMapSourceImage>();

    public DbSet<MapEditOperation> MapEditOperations => Set<MapEditOperation>();

    public DbSet<WorldClock> WorldClocks => Set<WorldClock>();

    public DbSet<Actor> Actors => Set<Actor>();
    public DbSet<PlayerTileKnowledge> PlayerTileKnowledge => Set<PlayerTileKnowledge>();
    public DbSet<NpcMemory> NpcMemories => Set<NpcMemory>();
    public DbSet<HousingConstruction> HousingConstructions => Set<HousingConstruction>();
    public DbSet<ResourceDeposit> ResourceDeposits => Set<ResourceDeposit>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<CityTerritoryTile> CityTerritoryTiles => Set<CityTerritoryTile>();
    public DbSet<Faction> Factions => Set<Faction>();
    public DbSet<FactionTerritoryTile> FactionTerritoryTiles => Set<FactionTerritoryTile>();
    public DbSet<WorldEvent> WorldEvents => Set<WorldEvent>();
    public DbSet<WorldConsequence> WorldConsequences => Set<WorldConsequence>();
    public DbSet<CustomContentDefinition> CustomContentDefinitions => Set<CustomContentDefinition>();
    public DbSet<CampaignSimulationSettings> CampaignSimulationSettings => Set<CampaignSimulationSettings>();

    public override int SaveChanges() => SaveChanges(acceptAllChangesOnSuccess: true);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var pendingEvents = CaptureDomainEvents();
        AddWorldEvents(pendingEvents.Events);
        var result = base.SaveChanges(acceptAllChangesOnSuccess);

        DispatchAndClearAsync(pendingEvents, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        return result;
    }

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default) =>
        SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        var pendingEvents = CaptureDomainEvents();
        AddWorldEvents(pendingEvents.Events);
        var result = await base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);

        await DispatchAndClearAsync(pendingEvents, cancellationToken);
        return result;
    }

    protected override void ConfigureConventions(
        ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Guid>()
            .HaveColumnType("uuid");

        configurationBuilder.Properties<DateTime>()
            .HaveColumnType("timestamp with time zone");

        configurationBuilder.Properties<DateTimeOffset>()
            .HaveColumnType("timestamp with time zone");

        configurationBuilder.Properties<Enum>()
            .HaveConversion<string>()
            .HaveMaxLength(64);

        configurationBuilder.Properties<JsonDocument>()
            .HaveColumnType("jsonb");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DefaultSchema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RpgWorldDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }

    private PendingDomainEvents CaptureDomainEvents()
    {
        var aggregates = ChangeTracker.Entries<AggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .Distinct()
            .ToArray();

        var events = aggregates
            .SelectMany(aggregate => aggregate.DomainEvents)
            .ToArray();

        return new PendingDomainEvents(aggregates, events);
    }

    private async Task DispatchAndClearAsync(
        PendingDomainEvents pending,
        CancellationToken cancellationToken)
    {
        if (pending.Events.Length == 0) return;

        foreach (var aggregate in pending.Aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        if (_domainEventDispatcher is not null)
            await _domainEventDispatcher.DispatchAsync(pending.Events, cancellationToken);
    }

    private void AddWorldEvents(IEnumerable<IDomainEvent> domainEvents)
    {
        var trackedIds = WorldEvents.Local.Select(worldEvent => worldEvent.Id).ToHashSet();
        var timelineEvents = domainEvents.Select(WorldEventPolicy.Create).OfType<WorldEvent>()
            .Where(worldEvent => trackedIds.Add(worldEvent.Id)).ToArray();
        if (timelineEvents.Length > 0) WorldEvents.AddRange(timelineEvents);
    }

    private sealed record PendingDomainEvents(
        AggregateRoot[] Aggregates,
        IDomainEvent[] Events);
}
