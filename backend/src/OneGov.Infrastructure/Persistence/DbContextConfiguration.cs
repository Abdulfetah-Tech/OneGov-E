using Microsoft.EntityFrameworkCore;

namespace OneGov.Infrastructure.Persistence;

/// <summary>Single place that configures the provider so runtime and design-time (migrations) agree.</summary>
public static class DbContextConfiguration
{
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention();
}
