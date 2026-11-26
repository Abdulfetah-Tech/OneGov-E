using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneGov.Infrastructure.Persistence;
using OneGov.Infrastructure.Services;
using Testcontainers.PostgreSql;

namespace OneGov.IntegrationTests;

/// <summary>One throw-away PostgreSQL container and one API host shared by the tests in <see cref="ApiCollection"/>.</summary>
public sealed class PostgresApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("onegov_test")
        .WithUsername("onegov_test")
        .WithPassword("local-test-only")
        .Build();

    private readonly string _documentsRoot = Path.Combine(Path.GetTempPath(), "onegov-test-documents-" + Guid.NewGuid().ToString("N"));

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public string DocumentsRoot => _documentsRoot;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = _postgres.GetConnectionString(),
                    ["NationalId:Provider"] = "mock",
                    ["Documents:RootPath"] = _documentsRoot,
                    ["Jwt:SigningKey"] = "test-only-signing-key-0123456789-abcdefghijklmnop",
                    // Tests share one client address, so the production limits would reject them.
                    ["RateLimiting:GlobalPermitLimit"] = "100000",
                    ["RateLimiting:AuthPermitLimit"] = "100000",
                }));
        });

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OneGovDbContext>();
        await DatabaseInitializer.InitializeAsync(db, applyMigrations: true, seedDevelopmentData: true, CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
        if (Directory.Exists(_documentsRoot))
        {
            Directory.Delete(_documentsRoot, recursive: true);
        }
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<PostgresApiFixture>
{
    public const string Name = "api";
}
