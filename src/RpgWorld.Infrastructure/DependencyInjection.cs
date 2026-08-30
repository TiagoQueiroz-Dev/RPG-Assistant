using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        return services;
    }
}

