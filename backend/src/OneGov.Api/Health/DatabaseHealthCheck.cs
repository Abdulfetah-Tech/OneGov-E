using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OneGov.Infrastructure.Persistence;

namespace OneGov.Api.Health;

public sealed class DatabaseHealthCheck(OneGovDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Database is not reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Database check failed.", exception);
        }
    }
}
