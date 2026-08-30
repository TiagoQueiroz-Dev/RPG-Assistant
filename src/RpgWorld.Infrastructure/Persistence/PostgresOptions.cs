using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace RpgWorld.Infrastructure.Persistence;

internal static class PostgresOptions
{
    public const string ConnectionStringName = "RpgWorld";

    public static void Configure(NpgsqlDbContextOptionsBuilder options)
    {
        options.MigrationsAssembly(typeof(RpgWorldDbContext).Assembly.FullName);
        options.MigrationsHistoryTable(
            "__ef_migrations_history",
            RpgWorldDbContext.DefaultSchema);
        options.EnableRetryOnFailure(maxRetryCount: 5);
    }
}

