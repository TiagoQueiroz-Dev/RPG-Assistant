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

namespace RpgWorld.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
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
