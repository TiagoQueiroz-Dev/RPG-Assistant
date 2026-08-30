using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RpgWorld.Infrastructure.Persistence;

public sealed class RpgWorldDbContextFactory
    : IDesignTimeDbContextFactory<RpgWorldDbContext>
{
    private const string DesignTimeConnectionString =
        "Host=localhost;Port=5432;Database=rpg_world;Username=rpg_world";

    public RpgWorldDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__RpgWorld");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = DesignTimeConnectionString;
        }

        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(connectionString, PostgresOptions.Configure)
            .Options;

        return new RpgWorldDbContext(options);
    }
}

