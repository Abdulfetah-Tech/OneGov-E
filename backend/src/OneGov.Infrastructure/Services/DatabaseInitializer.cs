using Microsoft.EntityFrameworkCore;
using OneGov.Infrastructure.Identity;
using OneGov.Infrastructure.Persistence;

namespace OneGov.Infrastructure.Services;

public static class DatabaseInitializer
{
    /// <summary>Migrations always run before any seeding, so a fresh database never hits missing tables.</summary>
    public static async Task InitializeAsync(
        OneGovDbContext db, bool applyMigrations, bool seedDevelopmentData, CancellationToken cancellationToken)
    {
        if (applyMigrations)
        {
            if (!db.Database.GetMigrations().Any())
            {
                throw new InvalidOperationException(
                    "No EF Core migrations were found. Create the first one: "
                    + "dotnet ef migrations add InitialFoundation --project backend/src/OneGov.Infrastructure "
                    + "--startup-project backend/src/OneGov.Api --output-dir Persistence/Migrations");
            }

            await db.Database.MigrateAsync(cancellationToken);
        }

        // Roles are required for registration to work, so they are created whether or not sample data is wanted.
        await IdentitySeeder.SeedRolesAsync(db, cancellationToken);

        if (seedDevelopmentData)
        {
            await DevelopmentDataSeeder.SeedAsync(db, cancellationToken);
            await IdentitySeeder.SeedDevelopmentUsersAsync(db, cancellationToken);
        }
    }
}
