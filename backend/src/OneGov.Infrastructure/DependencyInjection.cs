using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OneGov.Application.Audit;
using OneGov.Application.Registry;
using OneGov.Infrastructure.Persistence;
using OneGov.Infrastructure.Services;

namespace OneGov.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        // Resolved lazily so the final configuration (user secrets, env vars, test overrides) is always used.
        services.AddDbContext<OneGovDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString("Default")
                ?? throw new InvalidOperationException("Configure ConnectionStrings:Default.");
            DbContextConfiguration.Configure(options, connectionString);
        });

        services.AddScoped<IServiceCatalog, ServiceCatalog>();
        services.AddScoped<IAuditLog, AuditLog>();
        return services;
    }
}
