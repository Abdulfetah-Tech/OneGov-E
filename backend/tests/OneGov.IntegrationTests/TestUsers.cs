using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneGov.Domain.Identity;
using OneGov.Infrastructure.Identity;
using OneGov.Infrastructure.Persistence;

namespace OneGov.IntegrationTests;

/// <summary>A signed-in test user with an HTTP client that already carries their access token.</summary>
internal sealed record TestSession(string Email, Guid UserId, HttpClient Client);

/// <summary>Creates users for tests through the real API, so tests exercise the same sign-in paths as the web app.</summary>
internal static class TestUsers
{
    public const string Password = "Correct-Horse-9!";

    public static async Task<TestSession> CitizenAsync(PostgresApiFixture fixture)
    {
        var (email, userId) = await RegisterAsync(fixture);
        var token = await LoginTokenAsync(fixture, email, otp: null);
        return new TestSession(email, userId, Authenticated(fixture, token));
    }

    /// <summary>
    /// A staff member of one agency. With <paramref name="withMfa"/> they have enrolled an authenticator and signed in with a
    /// code, which staff permissions require. With <paramref name="alsoCitizen"/> they keep the citizen role too.
    /// </summary>
    public static async Task<TestSession> StaffAsync(
        PostgresApiFixture fixture, string role, Guid agencyId, bool withMfa = true, bool alsoCitizen = false)
    {
        var (email, userId) = await RegisterAsync(fixture);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(userId.ToString()))!;
            user.AgencyId = agencyId;
            Assert.True((await users.UpdateAsync(user)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
            if (!alsoCitizen)
            {
                Assert.True((await users.RemoveFromRoleAsync(user, Roles.Citizen)).Succeeded);
            }
        }

        var passwordToken = await LoginTokenAsync(fixture, email, otp: null);
        if (!withMfa)
        {
            return new TestSession(email, userId, Authenticated(fixture, passwordToken));
        }

        var enroll = await Authenticated(fixture, passwordToken)
            .PostAsJsonAsync("/api/v1/auth/mfa/enroll", new { password = Password });
        Assert.Equal(HttpStatusCode.OK, enroll.StatusCode);
        var key = (await enroll.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sharedKey").GetString()!;

        var confirm = await Authenticated(fixture, passwordToken)
            .PostAsJsonAsync("/api/v1/auth/mfa/confirm", new { code = Totp.Compute(key, DateTimeOffset.UtcNow) });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var mfaToken = await LoginTokenAsync(fixture, email, Totp.Compute(key, DateTimeOffset.UtcNow));
        return new TestSession(email, userId, Authenticated(fixture, mfaToken));
    }

    /// <summary>The agency that owns the sample tax filing service.</summary>
    public static async Task<Guid> TaxAgencyAsync(PostgresApiFixture fixture)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OneGovDbContext>();
        return await db.Services.Where(s => s.Code == "TAX-FILING").Select(s => s.AgencyId).FirstAsync();
    }

    /// <summary>Some other agency, for checking that agencies cannot see each other's applications.</summary>
    public static async Task<Guid> OtherAgencyAsync(PostgresApiFixture fixture)
    {
        var tax = await TaxAgencyAsync(fixture);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OneGovDbContext>();
        return await db.Agencies.Where(a => a.Id != tax).Select(a => a.Id).FirstAsync();
    }

    private static async Task<(string Email, Guid UserId)> RegisterAsync(PostgresApiFixture fixture)
    {
        var email = $"user-{Guid.NewGuid():N}@example.test";
        var response = await fixture.Factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/register", new { email, password = Password, displayName = "Test User" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (email, profile.GetProperty("id").GetGuid());
    }

    private static async Task<string> LoginTokenAsync(PostgresApiFixture fixture, string email, string? otp)
    {
        var response = await fixture.Factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password, otpCode = otp });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
    }

    private static HttpClient Authenticated(PostgresApiFixture fixture, string token)
    {
        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
