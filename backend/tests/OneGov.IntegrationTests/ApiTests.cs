using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneGov.Application.Audit;
using OneGov.Domain.Registry;
using OneGov.Infrastructure.Persistence;

namespace OneGov.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ApiTests(PostgresApiFixture fixture)
{
    private HttpClient Client => fixture.Factory.CreateClient();

    [Fact]
    public async Task Ready_endpoint_reports_a_healthy_database()
    {
        var response = await Client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Service_directory_lists_available_services_first()
    {
        var page = await Client.GetFromJsonAsync<JsonElement>("/api/v1/services?pageSize=50");

        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.True(items.Count >= 9);
        Assert.Equal("Available", items[0].GetProperty("status").GetString());
        Assert.Equal("Available", items[1].GetProperty("status").GetString());
        Assert.Contains(items, i => i.GetProperty("status").GetString() == "Planned");
    }

    [Fact]
    public async Task Search_and_category_filters_work()
    {
        var search = await Client.GetFromJsonAsync<JsonElement>("/api/v1/services?search=tax");
        var byCategory = await Client.GetFromJsonAsync<JsonElement>("/api/v1/services?category=Immigration");

        Assert.Equal("TAX-FILING", search.GetProperty("items")[0].GetProperty("code").GetString());
        Assert.Equal("PASSPORT-VISA", byCategory.GetProperty("items")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Search_treats_wildcards_as_plain_text()
    {
        var page = await Client.GetFromJsonAsync<JsonElement>("/api/v1/services?search=%25");

        Assert.Equal(0, page.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Amharic_names_are_returned_and_missing_text_falls_back_to_english()
    {
        var detail = await Client.GetFromJsonAsync<JsonElement>("/api/v1/services/TAX-FILING?language=am");

        Assert.Equal("የግብር ማስታወቂያ", detail.GetProperty("name").GetString());
        Assert.Equal("Registered taxpayer (sample rule)", detail.GetProperty("requirements")[0].GetProperty("text").GetString());
    }

    [Theory]
    [InlineData("/api/v1/services?language=fr")]
    [InlineData("/api/v1/services?page=0")]
    [InlineData("/api/v1/services?pageSize=500")]
    [InlineData("/api/v1/services?category=Nope")]
    public async Task Invalid_queries_return_problem_details(string url)
    {
        var response = await Client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Unknown_service_returns_404()
    {
        var response = await Client.GetAsync("/api/v1/services/DOES-NOT-EXIST");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Responses_carry_security_headers()
    {
        var response = await Client.GetAsync("/api/v1/services");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
    }

    [Fact]
    public async Task Audit_chain_survives_a_round_trip_and_detects_tampering()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditLog>();
        var db = scope.ServiceProvider.GetRequiredService<OneGovDbContext>();

        await audit.AppendAsync("officer-1", "citizen.read", "citizen/1", "support case 42", CancellationToken.None);
        await audit.AppendAsync("officer-2", "application.approve", "application/9", null, CancellationToken.None);
        Assert.True((await audit.VerifyAsync(CancellationToken.None)).IsValid);

        // Append-only is enforced in the application...
        var tracked = await db.AuditEntries.FirstAsync();
        db.Entry(tracked).State = EntityState.Deleted;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        // ...and tampering by someone who defeats the database trigger is still detected by the hash chain.
        // Only the table owner can do this, which is why the production app should connect as a different role.
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE audit_entries DISABLE TRIGGER audit_entries_append_only");
        await db.Database.ExecuteSqlAsync($"UPDATE audit_entries SET actor = 'mallory' WHERE sequence = 2");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE audit_entries ENABLE TRIGGER audit_entries_append_only");
        var verification = await audit.VerifyAsync(CancellationToken.None);

        Assert.False(verification.IsValid);
        Assert.Equal(2, verification.FirstBrokenSequence);
    }

    [Theory]
    [InlineData("UPDATE audit_entries SET actor = 'mallory'")]
    [InlineData("DELETE FROM audit_entries")]
    [InlineData("TRUNCATE audit_entries")]
    public async Task The_database_itself_refuses_to_change_audit_entries(string sql)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditLog>();
        var db = scope.ServiceProvider.GetRequiredService<OneGovDbContext>();
        await audit.AppendAsync("officer-3", "citizen.read", "citizen/3", null, CancellationToken.None);

        var error = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(sql));

        Assert.Contains("append-only", error.ToString());
    }

    [Fact]
    public async Task Seeded_services_reference_a_known_workflow()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OneGovDbContext>();

        var available = await db.Services.Where(s => s.Status == ServiceStatus.Available).ToListAsync();

        Assert.NotEmpty(available);
        Assert.All(available, s => Assert.NotNull(OneGov.Domain.Workflow.BuiltInWorkflows.All.FirstOrDefault(w => w.Code == s.WorkflowCode)));
    }
}
