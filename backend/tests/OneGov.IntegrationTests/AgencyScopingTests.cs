using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneGov.Application.Identity;
using OneGov.Domain.Applications;
using OneGov.Domain.Identity;
using OneGov.Domain.Workflow;
using OneGov.Infrastructure.Persistence;

namespace OneGov.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AgencyScopingTests(PostgresApiFixture fixture)
{
    private sealed class FixedScope(DataScope scope) : IDataScopeAccessor
    {
        public DataScope Current => scope;
    }

    /// <summary>Two citizens, two agencies: A for citizen X, A for citizen Y, B for citizen X.</summary>
    private sealed record Seeded(Guid AgencyA, Guid AgencyB, Guid CitizenX, Guid CitizenY, Guid AppAx, Guid AppAy, Guid AppBx);

    private static OneGovDbContext NewDb(DbContextOptions<OneGovDbContext> options, DataScope scope) =>
        new(options, TimeProvider.System, new FixedScope(scope));

    private async Task<Seeded> SeedAsync(DbContextOptions<OneGovDbContext> options)
    {
        await using var db = NewDb(options, DataScope.Everything);
        var agencies = await db.Agencies.OrderBy(a => a.Code).Take(2).ToListAsync();
        var service = await db.Services.FirstAsync();
        var citizenX = Guid.NewGuid();
        var citizenY = Guid.NewGuid();
        var workflow = BuiltInWorkflows.TaxFiling;

        var ax = ServiceApplication.CreateDraft(service.Id, agencies[0].Id, citizenX, workflow);
        var ay = ServiceApplication.CreateDraft(service.Id, agencies[0].Id, citizenY, workflow);
        var bx = ServiceApplication.CreateDraft(service.Id, agencies[1].Id, citizenX, workflow);
        db.Applications.AddRange(ax, ay, bx);
        await db.SaveChangesAsync();

        return new Seeded(agencies[0].Id, agencies[1].Id, citizenX, citizenY, ax.Id, ay.Id, bx.Id);
    }

    private static async Task<List<Guid>> VisibleAsync(DbContextOptions<OneGovDbContext> options, DataScope scope)
    {
        await using var db = NewDb(options, scope);
        return await db.Applications.Select(a => a.Id).ToListAsync();
    }

    [Fact]
    public async Task Callers_only_see_the_applications_their_scope_allows()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<OneGovDbContext>>();
        var s = await SeedAsync(options);

        // A citizen sees their own applications in every agency, and nobody else's.
        var citizenX = await VisibleAsync(options, DataScope.For(s.CitizenX, null, [Roles.Citizen], mfaVerified: false));
        Assert.Contains(s.AppAx, citizenX);
        Assert.Contains(s.AppBx, citizenX);
        Assert.DoesNotContain(s.AppAy, citizenX);

        // An officer of agency A sees every citizen's applications there, and nothing from agency B.
        var officerA = await VisibleAsync(options, DataScope.For(Guid.NewGuid(), s.AgencyA, [Roles.Officer], mfaVerified: true));
        Assert.Contains(s.AppAx, officerA);
        Assert.Contains(s.AppAy, officerA);
        Assert.DoesNotContain(s.AppBx, officerA);

        // The same officer without a second factor sees nothing.
        Assert.Empty(await VisibleAsync(options, DataScope.For(Guid.NewGuid(), s.AgencyA, [Roles.Officer], mfaVerified: false)));

        // Administrators and anonymous callers see no applications.
        Assert.Empty(await VisibleAsync(options, DataScope.For(Guid.NewGuid(), null, [Roles.PlatformAdmin], mfaVerified: true)));
        Assert.Empty(await VisibleAsync(options, DataScope.None));

        // Trusted system code sees everything.
        var everything = await VisibleAsync(options, DataScope.Everything);
        Assert.Contains(s.AppAx, everything);
        Assert.Contains(s.AppAy, everything);
        Assert.Contains(s.AppBx, everything);
    }

    [Fact]
    public async Task A_context_with_no_scope_at_all_sees_no_applications()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<OneGovDbContext>>();
        await SeedAsync(options);

        await using var db = new OneGovDbContext(options, TimeProvider.System);

        Assert.Empty(await db.Applications.ToListAsync());
    }

    [Fact]
    public async Task Requests_through_the_api_context_see_no_applications_when_anonymous()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<OneGovDbContext>>();
        await SeedAsync(options);

        // Resolved from the container, outside any HTTP request: the real accessor must fail closed.
        var db = scope.ServiceProvider.GetRequiredService<OneGovDbContext>();

        Assert.Empty(await db.Applications.ToListAsync());
    }
}
