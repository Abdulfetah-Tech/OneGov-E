using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OneGov.Application.Applications;
using OneGov.Application.Documents;
using OneGov.Application.Audit;
using OneGov.Application.Identity;
using OneGov.Application.Notifications;
using OneGov.Application.Registry;
using OneGov.Infrastructure.Identity;
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

        // Identity's token providers need data protection. Keys persist in the api_keys volume under Docker.
        services.AddDataProtection();

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                // Length over composition rules. A breached-password check is planned (docs/SECURITY_BASELINE.md).
                options.Password.RequiredLength = 10;
                options.Password.RequiredUniqueChars = 5;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<OneGovDbContext>()
            .AddDefaultTokenProviders();

        services.AddSingleton<JwtTokenIssuer>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IApplicationService, ApplicationService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IDocumentService, DocumentService>();

        // Uploaded files live below Documents:RootPath (default data/documents next to the app). Resolved lazily so overrides apply.
        services.AddSingleton<IDocumentStore>(provider =>
        {
            var configured = provider.GetRequiredService<IConfiguration>()["Documents:RootPath"] ?? "data/documents";
            var contentRoot = provider.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>().ContentRootPath;
            return new FileSystemDocumentStore(Path.GetFullPath(configured, contentRoot));
        });
        services.AddScoped<IServiceCatalog, ServiceCatalog>();
        services.AddScoped<IAuditLog, AuditLog>();
        return services;
    }
}
