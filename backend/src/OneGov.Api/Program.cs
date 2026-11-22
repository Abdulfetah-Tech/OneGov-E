using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using OneGov.Api.Health;
using OneGov.Api.Middleware;
using OneGov.Api.Security;
using OneGov.Application;
using OneGov.Infrastructure;
using OneGov.Infrastructure.Identity;
using OneGov.Infrastructure.Persistence;
using OneGov.Infrastructure.Services;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, logger) => logger
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "OneGov")
    .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
    .WriteTo.Console());

builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<OneGov.Application.Identity.IDataScopeAccessor, HttpDataScopeAccessor>();
builder.Services.AddOneGovJwtAuthentication();
builder.Services.AddNationalIdSignIn();
builder.Services.AddPermissionAuthorization();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddHealthChecks()
    .AddCheck("live", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<DatabaseHealthCheck>("postgresql", tags: ["ready"]);

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
}).AddMvc();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (allowedOrigins.Length == 0)
{
    throw new InvalidOperationException("Configure at least one origin in Cors:AllowedOrigins.");
}

builder.Services.AddCors(options =>
    options.AddPolicy("ConfiguredOrigins", policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

// Partitioned by client IP. Behind a reverse proxy set ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
// so the real client address is used, not the proxy's. Limits are read lazily so tests and deployments can override them.
builder.Services.AddRateLimiter(options => options.RejectionStatusCode = StatusCodes.Status429TooManyRequests);
builder.Services.AddOptions<RateLimiterOptions>().Configure<IConfiguration>((options, configuration) =>
{
    var globalLimit = configuration.GetValue("RateLimiting:GlobalPermitLimit", 100);
    var authLimit = configuration.GetValue("RateLimiting:AuthPermitLimit", 10);

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = globalLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));

    // Sign-in, registration and token endpoints get a much tighter limit than the rest of the API.
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
});

var app = builder.Build();

// Fail fast on a missing or weak signing key instead of on the first sign-in.
_ = app.Services.GetRequiredService<JwtOptions>();
_ = app.Services.GetRequiredService<OneGov.Application.Identity.INationalIdProvider>();

app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=()";
        if (!app.Environment.IsDevelopment())
        {
            headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
        }

        return Task.CompletedTask;
    });
    await next();
});
app.UseSerilogRequestLogging();
app.UseCors("ConfiguredOrigins");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = HealthResponseWriter.WriteAsync,
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.WriteAsync,
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

if (!app.Environment.IsEnvironment("Testing"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<OneGovDbContext>();
    await DatabaseInitializer.InitializeAsync(
        db,
        applyMigrations: app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"),
        seedDevelopmentData: app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Database:SeedDevelopmentData"),
        app.Lifetime.ApplicationStopping);
}

await app.RunAsync();

public partial class Program
{
}
