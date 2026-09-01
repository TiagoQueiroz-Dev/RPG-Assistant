using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RpgWorld.Application.Caching;
using RpgWorld.Application.Events;
using RpgWorld.Application.Worlds;
using RpgWorld.Application.Worlds.Importing;
using RpgWorld.Infrastructure.Caching;
using RpgWorld.Infrastructure.Events;
using RpgWorld.Infrastructure.Persistence;
using RpgWorld.Infrastructure.Persistence.Repositories;
using RpgWorld.Infrastructure.Worlds.Importing;
using RpgWorld.Infrastructure.Worlds.Editing;
using RpgWorld.Application.Worlds.Editing;
using RpgWorld.Application.Actors;
using RpgWorld.Application.Actors.Movement;
using RpgWorld.Application.Actors.Inspection;
using RpgWorld.Application.Actors.Memories;
using RpgWorld.Domain.Events;
using RpgWorld.Application.Actors.Relationships;
using RpgWorld.Application.Actors.Housing;
using RpgWorld.Application.Worlds.Resources;
using RpgWorld.Application.Worlds.Cities;
using RpgWorld.Application.Worlds.Factions;
using RpgWorld.Application.Worlds.Events;
using RpgWorld.Application.Worlds.Admin;
using RpgWorld.Infrastructure.Worlds.Admin;
using RpgWorld.Application.Worlds.Visibility;
using RpgWorld.Infrastructure.Worlds.Visibility;
using RpgWorld.Application.Worlds.Content;
using RpgWorld.Infrastructure.Worlds.Content;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RpgWorld.Infrastructure.Worlds;

namespace RpgWorld.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        NpcMemoryOptions? npcMemoryOptions = null)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddDbContext<RpgWorldDbContext>((serviceProvider, options) =>
        {
            var runtimeConfiguration = serviceProvider
                .GetRequiredService<IConfiguration>();
            var connectionString = runtimeConfiguration.GetConnectionString(
                PostgresOptions.ConnectionStringName);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Connection string 'RpgWorld' is required. Set it through " +
                    "ConnectionStrings__RpgWorld for the current environment.");
            }

            options.UseNpgsql(connectionString, PostgresOptions.Configure);
        });
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IWorldMapRepository, EfWorldMapRepository>();
        services.AddScoped<IWorldImportService, WorldImportService>();
        services.AddSingleton<IMapRegionClassifier, ColorMapRegionClassifier>();
        services.AddScoped<IWorldClassificationService, WorldClassificationService>();
        services.AddScoped<IMapEditingService, MapEditingService>();
        services.AddScoped<IWorldClockRepository, EfWorldClockRepository>();
        services.AddScoped<IWorldSimulationRepository, EfWorldSimulationRepository>();
        services.AddScoped<IRegionSimulationRepository, EfRegionSimulationRepository>();
        services.AddScoped<IActorRepository, EfActorRepository>();
        services.AddScoped<IActorMovementStore, EfActorMovementStore>();
        services.AddScoped<INpcNeedsRepository, EfNpcNeedsRepository>();
        services.AddScoped<INpcInspectorService, NpcInspectorService>();
        services.AddScoped<INpcMemoryRepository, EfNpcMemoryRepository>();
        services.AddScoped<IActorRelationshipService, ActorRelationshipService>();
        services.AddScoped<INpcHousingRepository, EfNpcHousingRepository>();
        services.AddScoped<INaturalResourceRepository, EfNaturalResourceRepository>();
        services.AddScoped<INaturalResourceService, NaturalResourceService>();
        services.AddScoped<IDomainEventHandler<NaturalResourceEmergenceEvent>, NaturalResourceEmergenceHandler>();
        services.AddScoped<ICityRepository, EfCityRepository>();
        services.AddScoped<ICityEconomyRepository, EfCityEconomyRepository>();
        services.AddScoped<ICityService, CityService>();
        services.AddScoped<IFactionRepository, EfFactionRepository>();
        services.AddScoped<IFactionWarRepository, EfFactionRepository>();
        services.AddScoped<IFactionService, FactionService>();
        services.AddScoped<IWorldEventRepository, EfWorldEventRepository>();
        services.AddScoped<IWorldEventService, WorldEventService>();
        services.AddScoped<IWorldConsequenceRepository, EfWorldConsequenceRepository>();
        services.AddScoped<IDomainEventHandler<ActorKilledEvent>, ActorKilledReputationConsequenceHandler>();
        services.AddScoped<IDomainEventHandler<ActorKilledEvent>, ActorKilledCrimeConsequenceHandler>();
        services.AddScoped<IDomainEventHandler<ActorKilledEvent>, ActorKilledFamilyConsequenceHandler>();
        services.AddScoped<IDomainEventHandler<ActorKilledEvent>, ActorKilledFactionConsequenceHandler>();
        services.AddScoped<IDomainEventHandler<ActorKilledEvent>, ActorKilledEconomyConsequenceHandler>();
        services.AddScoped<IDomainEventHandler<WorldConsequenceAppliedEvent>, CrimeFactionEscalationHandler>();
        services.AddScoped<IDomainEventHandler<WorldConsequenceAppliedEvent>, FactionEconomyEscalationHandler>();
        services.AddScoped<IWorldAdminRepository, EfWorldAdminRepository>();
        services.AddScoped<IWorldAdminService, WorldAdminService>();
        services.AddScoped<IWorldMapLayerRepository, EfWorldMapLayerRepository>();
        services.AddScoped<IWorldMapLayerService, WorldMapLayerService>();
        services.AddScoped<IGameMasterCommandService, GameMasterCommandService>();
        services.AddScoped<IPlayerVisibilityService, PlayerVisibilityService>();
        services.AddScoped<IPlayerWorldViewService, PlayerWorldViewService>();
        services.AddScoped<IPlayerCurrentRegionService, PlayerCurrentRegionService>();
        services.AddScoped<CustomContentService>();
        services.AddScoped<ICustomContentService>(provider => provider.GetRequiredService<CustomContentService>());
        services.AddScoped<ICampaignContentCatalogProvider>(provider => provider.GetRequiredService<CustomContentService>());
        services.AddScoped<CampaignSimulationSettingsService>();
        services.AddScoped<ICampaignSimulationSettingsService>(provider =>
            provider.GetRequiredService<CampaignSimulationSettingsService>());
        services.AddScoped<ICampaignSimulationSettingsProvider>(provider =>
            provider.GetRequiredService<CampaignSimulationSettingsService>());
        services.AddScoped<IDomainEventHandler<ActorCreatedEvent>, PlayerVisibilityCreatedEventHandler>();
        services.AddScoped<IDomainEventHandler<ActorMovedEvent>, PlayerVisibilityMovedEventHandler>();
        var effectiveNpcMemoryOptions = npcMemoryOptions ?? new NpcMemoryOptions();
        effectiveNpcMemoryOptions.Validate();
        services.AddSingleton(effectiveNpcMemoryOptions);
        services.AddScoped<NpcMemoryEventRecorder>();
        services.AddScoped<IDomainEventHandler<ActorDamagedEvent>, NpcDamagedMemoryHandler>();
        services.AddScoped<IDomainEventHandler<ActorKilledEvent>, NpcFamilyKilledMemoryHandler>();

        AddCaching(services, configuration);

        return services;
    }

    private static void AddCaching(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var redisOptions = RedisOptions.FromConfiguration(configuration);
        services.AddSingleton(redisOptions);

        if (!redisOptions.Enabled)
        {
            services.AddSingleton<ICacheService, NoOpCacheService>();
            return;
        }

        if (string.IsNullOrWhiteSpace(redisOptions.ConnectionString))
        {
            throw new InvalidOperationException(
                "Redis is enabled but Redis:ConnectionString is missing. " +
                "Set Redis__ConnectionString for the current environment.");
        }

        services.AddSingleton<ICacheService, RedisCacheService>();
    }
}
