using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OneGov.Domain.Identity;
using OneGov.Infrastructure.Identity;

namespace OneGov.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AuthTests(PostgresApiFixture fixture)
{
    private const string Password = "Correct-Horse-9!";

    private HttpClient Client => fixture.Factory.CreateClient();

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.test";

    private static Task<HttpResponseMessage> Post(HttpClient client, string path, object body) =>
        client.PostAsJsonAsync(path, body);

    private async Task<string> RegisterAsync(string? email = null)
    {
        email ??= NewEmail();
        var response = await Post(Client, "/api/v1/auth/register", new { email, password = Password, displayName = "Test User" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return email;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<JsonElement> LoginOkAsync(string email, string? otp = null)
    {
        var response = await Post(Client, "/api/v1/auth/login", new { email, password = Password, otpCode = otp });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await Json(response);
    }

    private HttpClient Authenticated(string accessToken)
    {
        var client = Client;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private async Task AddRoleAsync(string email, string role)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
    }

    [Fact]
    public async Task Registration_creates_a_citizen()
    {
        var email = NewEmail();

        var response = await Post(Client, "/api/v1/auth/register", new { email, password = Password, displayName = "Abebe", language = "am" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var profile = await Json(response);
        Assert.Equal(email, profile.GetProperty("email").GetString());
        Assert.Equal("am", profile.GetProperty("language").GetString());
        Assert.False(profile.GetProperty("mfaEnabled").GetBoolean());
        Assert.Contains("citizen", profile.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        Assert.Contains("application:submit", profile.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()));
    }

    [Fact]
    public async Task Registration_rejects_a_duplicate_email()
    {
        var email = await RegisterAsync();

        var response = await Post(Client, "/api/v1/auth/register", new { email, password = Password, displayName = "Again" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("identity.email_taken", (await Json(response)).GetProperty("type").GetString());
    }

    [Theory]
    [InlineData("short", "identity.weak_password")]
    [InlineData("aaaaaaaaaaaa", "identity.weak_password")]
    public async Task Registration_rejects_weak_passwords(string password, string expectedType)
    {
        var response = await Post(Client, "/api/v1/auth/register", new { email = NewEmail(), password, displayName = "Test" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedType, (await Json(response)).GetProperty("type").GetString());
    }

    [Fact]
    public async Task Registration_rejects_an_invalid_email()
    {
        var response = await Post(Client, "/api/v1/auth/register", new { email = "not-an-email", password = Password, displayName = "Test" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("identity.invalid_request", (await Json(response)).GetProperty("type").GetString());
    }

    [Fact]
    public async Task Login_returns_tokens_that_open_the_profile_endpoint()
    {
        var email = await RegisterAsync();

        var tokens = await LoginOkAsync(email);
        var me = await Authenticated(tokens.GetProperty("accessToken").GetString()!).GetAsync("/api/v1/me");

        Assert.Equal("Bearer", tokens.GetProperty("tokenType").GetString());
        Assert.False(string.IsNullOrEmpty(tokens.GetProperty("refreshToken").GetString()));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(email, (await Json(me)).GetProperty("email").GetString());
    }

    [Fact]
    public async Task Auth_responses_are_not_cacheable()
    {
        var email = await RegisterAsync();

        var response = await Post(Client, "/api/v1/auth/login", new { email, password = Password });

        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Wrong_passwords_and_unknown_accounts_look_identical()
    {
        var email = await RegisterAsync();

        var wrongPassword = await Post(Client, "/api/v1/auth/login", new { email, password = "Wrong-Password-1" });
        var unknownAccount = await Post(Client, "/api/v1/auth/login", new { email = NewEmail(), password = Password });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownAccount.StatusCode);
        var a = await Json(wrongPassword);
        var b = await Json(unknownAccount);
        Assert.Equal(a.GetProperty("type").GetString(), b.GetProperty("type").GetString());
        Assert.Equal(a.GetProperty("detail").GetString(), b.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task The_profile_endpoint_requires_a_valid_token()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Authenticated("not-a-token").GetAsync("/api/v1/me")).StatusCode);
    }

    [Fact]
    public async Task Accounts_lock_after_repeated_failures()
    {
        var email = await RegisterAsync();

        for (var i = 0; i < 5; i++)
        {
            await Post(Client, "/api/v1/auth/login", new { email, password = "Wrong-Password-1" });
        }

        var correctButLocked = await Post(Client, "/api/v1/auth/login", new { email, password = Password });

        Assert.Equal(HttpStatusCode.Unauthorized, correctButLocked.StatusCode);
    }

    [Fact]
    public async Task Refreshing_rotates_the_token_and_reuse_revokes_the_whole_family()
    {
        var email = await RegisterAsync();
        var first = (await LoginOkAsync(email)).GetProperty("refreshToken").GetString()!;

        var rotated = await Post(Client, "/api/v1/auth/refresh", new { refreshToken = first });
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var second = (await Json(rotated)).GetProperty("refreshToken").GetString()!;
        Assert.NotEqual(first, second);

        // Using the old token again means it leaked: it is refused and the family is revoked...
        var replay = await Post(Client, "/api/v1/auth/refresh", new { refreshToken = first });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        // ...including the token that had just been issued.
        var afterReplay = await Post(Client, "/api/v1/auth/refresh", new { refreshToken = second });
        Assert.Equal(HttpStatusCode.Unauthorized, afterReplay.StatusCode);
    }

    [Fact]
    public async Task Concurrent_refreshes_of_one_token_cannot_both_succeed()
    {
        var email = await RegisterAsync();
        var token = (await LoginOkAsync(email)).GetProperty("refreshToken").GetString()!;

        var responses = await Task.WhenAll(
            Post(Client, "/api/v1/auth/refresh", new { refreshToken = token }),
            Post(Client, "/api/v1/auth/refresh", new { refreshToken = token }));

        Assert.True(responses.Count(r => r.StatusCode == HttpStatusCode.OK) <= 1);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var email = await RegisterAsync();
        var refreshToken = (await LoginOkAsync(email)).GetProperty("refreshToken").GetString()!;

        var logout = await Post(Client, "/api/v1/auth/logout", new { refreshToken });
        var refresh = await Post(Client, "/api/v1/auth/refresh", new { refreshToken });

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_does_not_reveal_whether_a_token_was_valid()
    {
        var logout = await Post(Client, "/api/v1/auth/logout", new { refreshToken = "never-issued" });

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
    }

    [Fact]
    public async Task Citizens_cannot_read_the_audit_verification()
    {
        var email = await RegisterAsync();
        var token = (await LoginOkAsync(email)).GetProperty("accessToken").GetString()!;

        var response = await Authenticated(token).GetAsync("/api/v1/audit/verify");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Staff_permissions_need_a_second_factor_and_recovery_codes_work_once()
    {
        var email = await RegisterAsync();
        await AddRoleAsync(email, Roles.PlatformAdmin);

        // Right role, but a password-only sign-in is not enough for a staff permission.
        var passwordOnly = (await LoginOkAsync(email)).GetProperty("accessToken").GetString()!;
        Assert.Equal(HttpStatusCode.Forbidden, (await Authenticated(passwordOnly).GetAsync("/api/v1/audit/verify")).StatusCode);

        // Enrol an authenticator app.
        var enrol = await Post(Authenticated(passwordOnly), "/api/v1/auth/mfa/enroll", new { password = Password });
        Assert.Equal(HttpStatusCode.OK, enrol.StatusCode);
        var enrolment = await Json(enrol);
        var key = enrolment.GetProperty("sharedKey").GetString()!;
        Assert.StartsWith("otpauth://totp/OneGov:", enrolment.GetProperty("otpAuthUri").GetString());

        var wrongCode = await Post(Authenticated(passwordOnly), "/api/v1/auth/mfa/confirm", new { code = "000000" });
        Assert.Equal(HttpStatusCode.BadRequest, wrongCode.StatusCode);

        var confirm = await Post(
            Authenticated(passwordOnly), "/api/v1/auth/mfa/confirm", new { code = Totp.Compute(key, DateTimeOffset.UtcNow) });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var recoveryCodes = (await Json(confirm)).GetProperty("recoveryCodes").EnumerateArray().Select(c => c.GetString()!).ToList();
        Assert.Equal(10, recoveryCodes.Count);

        // From now on a code is required.
        var withoutCode = await Post(Client, "/api/v1/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, withoutCode.StatusCode);
        Assert.Equal("identity.mfa_required", (await Json(withoutCode)).GetProperty("type").GetString());

        var withCode = await LoginOkAsync(email, Totp.Compute(key, DateTimeOffset.UtcNow));
        var mfaToken = withCode.GetProperty("accessToken").GetString()!;
        var verify = await Authenticated(mfaToken).GetAsync("/api/v1/audit/verify");
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        Assert.True((await Json(verify)).TryGetProperty("isValid", out _));

        // The second factor survives a refresh, so staff are not silently downgraded.
        var refreshed = await Post(Client, "/api/v1/auth/refresh", new { refreshToken = withCode.GetProperty("refreshToken").GetString() });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var refreshedToken = (await Json(refreshed)).GetProperty("accessToken").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await Authenticated(refreshedToken).GetAsync("/api/v1/audit/verify")).StatusCode);

        // A recovery code signs in once and then is spent.
        var recovery = recoveryCodes[0];
        Assert.Equal(HttpStatusCode.OK, (await Post(Client, "/api/v1/auth/login", new { email, password = Password, otpCode = recovery })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(Client, "/api/v1/auth/login", new { email, password = Password, otpCode = recovery })).StatusCode);
    }

    [Fact]
    public async Task Starting_enrolment_needs_the_current_password()
    {
        var email = await RegisterAsync();
        var token = (await LoginOkAsync(email)).GetProperty("accessToken").GetString()!;

        var wrong = await Post(Authenticated(token), "/api/v1/auth/mfa/enroll", new { password = "Wrong-Password-1" });
        var missing = await Post(Authenticated(token), "/api/v1/auth/mfa/enroll", new { });

        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
        Assert.Equal("identity.reauthentication_failed", (await Json(wrong)).GetProperty("type").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Fact]
    public async Task Enrolment_cannot_be_started_twice_once_enabled()
    {
        var email = await RegisterAsync();
        var token = (await LoginOkAsync(email)).GetProperty("accessToken").GetString()!;
        var key = (await Json(await Post(Authenticated(token), "/api/v1/auth/mfa/enroll", new { password = Password }))).GetProperty("sharedKey").GetString()!;
        await Post(Authenticated(token), "/api/v1/auth/mfa/confirm", new { code = Totp.Compute(key, DateTimeOffset.UtcNow) });

        var again = await Post(Authenticated(token), "/api/v1/auth/mfa/enroll", new { password = Password });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Development_accounts_are_seeded_with_their_roles()
    {
        var response = await Post(Client, "/api/v1/auth/login", new { email = "officer@example.test", password = IdentitySeeder.DevelopmentPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var token = (await Json(response)).GetProperty("accessToken").GetString()!;
        var profile = await Json(await Authenticated(token).GetAsync("/api/v1/me"));

        Assert.Contains("officer", profile.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        Assert.NotEqual(JsonValueKind.Null, profile.GetProperty("agencyId").ValueKind);
    }
}
