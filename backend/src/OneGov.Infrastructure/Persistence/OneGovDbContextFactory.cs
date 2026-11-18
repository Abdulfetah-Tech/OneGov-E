using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OneGov.Infrastructure.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c> so migrations can be created without booting the web host.
/// It never opens a connection. Override with ONEGOV_CONNECTION if needed.
/// </summary>
public sealed class OneGovDbContextFactory : IDesignTimeDbContextFactory<OneGovDbContext>
{
    public OneGovDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ONEGOV_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=onegov;Username=onegov";
        var builder = new DbContextOptionsBuilder<OneGovDbContext>();
        DbContextConfiguration.Configure(builder, connectionString);
        return new OneGovDbContext(builder.Options, TimeProvider.System);
    }
}
