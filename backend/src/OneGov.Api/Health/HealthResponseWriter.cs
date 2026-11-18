using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OneGov.Api.Health;

/// <summary>Writes status only: no exception text or connection details leave the process.</summary>
public static class HealthResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Status.ToString().ToLowerInvariant()),
        }));
    }
}
