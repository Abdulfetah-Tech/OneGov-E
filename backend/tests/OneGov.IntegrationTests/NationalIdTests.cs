using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using OneGov.Infrastructure.Identity;

namespace OneGov.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class NationalIdTests(PostgresApiFixture fixture)
{
    private HttpClient Client => fixture.Factory.CreateClient();

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<string> StartAsync()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/national-id/start", new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await Json(response)).GetProperty("state").GetString()!;
    }

    private Task<HttpResponseMessage> CompleteAsync(string code, string state) =>
        Client.PostAsJsonAsync("/api/v1/auth/national-id/complete", new { code, state });

    private async Task<JsonElement> SignInAsync(string idNumber, string name)
    {
        var response = await CompleteAsync(MockNationalIdProvider.BuildCode(idNumber, name), await StartAsync());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await Json(response);
    }

    private async Task<JsonElement> ProfileAsync(JsonElement tokens)
    {
        var client = Client;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.GetProperty("accessToken").GetString());
        var response = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await Json(response);
    }

    [Fact]
    public async Task Status_reports_that_the_mock_provider_is_available()
    {
        var status = await Json(await Client.GetAsync("/api/v1/auth/national-id/status"));

        Assert.True(status.GetProperty("available").GetBoolean());
        Assert.True(status.GetProperty("isMock").GetBoolean());
    }

    [Fact]
    public async Task Start_returns_the_provider_address_and_a_state()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/national-id/start", new { });
        var body = await Json(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/auth/national-id/mock?state=", body.GetProperty("authorizationUrl").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("state").GetString()));
    }

    [Fact]
    public async Task A_first_sign_in_creates_a_citizen_and_the_same_id_returns_to_the_same_account()
    {
        var idNumber = Guid.NewGuid().ToString("N");

        var first = await ProfileAsync(await SignInAsync(idNumber, "አበበ በቀለ"));
        var second = await ProfileAsync(await SignInAsync(idNumber, "A different name"));

        Assert.Equal("አበበ በቀለ", first.GetProperty("displayName").GetString());
        Assert.Contains("citizen", first.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        Assert.EndsWith("@national-id.invalid", first.GetProperty("email").GetString());
        Assert.Equal(first.GetProperty("id").GetString(), second.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Different_ids_get_different_accounts()
    {
        var a = await ProfileAsync(await SignInAsync(Guid.NewGuid().ToString("N"), "Person A"));
        var b = await ProfileAsync(await SignInAsync(Guid.NewGuid().ToString("N"), "Person B"));

        Assert.NotEqual(a.GetProperty("id").GetString(), b.GetProperty("id").GetString());
    }

    [Fact]
    public async Task A_national_id_account_has_no_password_to_sign_in_with()
    {
        var profile = await ProfileAsync(await SignInAsync(Guid.NewGuid().ToString("N"), "Person"));

        var response = await Client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email = profile.GetProperty("email").GetString(), password = "Any-Password-123" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_national_id_sign_in_never_grants_staff_permissions()
    {
        var tokens = await SignInAsync(Guid.NewGuid().ToString("N"), "Person");
        var client = Client;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.GetProperty("accessToken").GetString());

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/audit/verify")).StatusCode);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("A")]
    public async Task A_state_that_was_not_issued_by_the_api_is_refused(string state)
    {
        var response = await CompleteAsync(MockNationalIdProvider.BuildCode("123", "Person"), state);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("identity.invalid_state", (await Json(response)).GetProperty("type").GetString());
    }

    [Fact]
    public async Task A_tampered_state_is_refused()
    {
        var state = await StartAsync();
        var tampered = state[..^2] + (state.EndsWith("AA", StringComparison.Ordinal) ? "BB" : "AA");

        var response = await CompleteAsync(MockNationalIdProvider.BuildCode("123", "Person"), tampered);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("nonsense")]
    [InlineData("mock.not-base64!!")]
    [InlineData("mock.e30")]
    public async Task A_code_the_provider_would_not_issue_is_refused(string code)
    {
        var response = await CompleteAsync(code, await StartAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("identity.national_id_failed", (await Json(response)).GetProperty("type").GetString());
    }
}
