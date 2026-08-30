using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RpgWorld.Application.Caching;
using RpgWorld.Application.Events;
using RpgWorld.Infrastructure.Caching;
using RpgWorld.Infrastructure.Events;
using RpgWorld.Infrastructure.Persistence;

namespace RpgWorld.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(
            PostgresOptions.ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'RpgWorld' is required. Set it through " +
                "ConnectionStrings__RpgWorld for the current environment.");
        }

        services.AddDbContext<RpgWorldDbContext>(options =>
            options.UseNpgsql(connectionString, PostgresOptions.Configure));
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

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
