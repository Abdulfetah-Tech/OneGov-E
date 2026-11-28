using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using OneGov.Domain.Identity;

namespace OneGov.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class NotificationsTests(PostgresApiFixture fixture)
{
    private const string Notifications = "/api/v1/notifications";

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<Guid> SubmittedApplicationAsync(TestSession citizen)
    {
        var created = await citizen.Client.PostAsJsonAsync("/api/v1/applications", new { serviceCode = "TAX-FILING" });
        var id = (await Json(created)).GetProperty("id").GetGuid();
        await Act(citizen, id, "submit");
        return id;
    }

    private static async Task Act(TestSession who, Guid id, string transition, string? note = null)
    {
        if (transition == "submit")
        {
            await TestForms.SaveValidTaxPayloadAsync(who, id);
        }

        var response = await who.Client.PostAsJsonAsync($"/api/v1/applications/{id}/transitions", new { transition, note });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<int> UnreadAsync(TestSession who)
    {
        var response = await who.Client.GetAsync($"{Notifications}/unread-count");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await Json(response)).GetProperty("count").GetInt32();
    }

    private static async Task<List<JsonElement>> ListAsync(TestSession who, string query = "")
    {
        var response = await who.Client.GetAsync($"{Notifications}{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await Json(response)).GetProperty("items").EnumerateArray().ToList();
    }

    [Fact]
    public async Task Submitting_gives_the_applicant_a_confirmation_with_the_reference_number()
    {
        var citizen = await TestUsers.CitizenAsync(fixture);
        var id = await SubmittedApplicationAsync(citizen);

        var items = await ListAsync(citizen);

        var confirmation = Assert.Single(items);
        Assert.Equal(id, confirmation.GetProperty("applicationId").GetGuid());
        Assert.Equal("submit", confirmation.GetProperty("transition").GetString());
        Assert.Equal("submitted", confirmation.GetProperty("toState").GetString());
        Assert.Matches(new Regex(@"^OG-TAXFILING-\d{4}-\d{6}$"), confirmation.GetProperty("referenceNumber").GetString()!);
        Assert.False(confirmation.GetProperty("isRead").GetBoolean());
        Assert.Equal(1, await UnreadAsync(citizen));
    }

    [Fact]
    public async Task Staff_steps_notify_the_applicant_newest_first_and_carry_the_note()
    {
        var agency = await TestUsers.TaxAgencyAsync(fixture);
        var citizen = await TestUsers.CitizenAsync(fixture);
        var officer = await TestUsers.StaffAsync(fixture, Roles.Officer, agency);
        var id = await SubmittedApplicationAsync(citizen);

        await Act(officer, id, "start_review");
        await Act(officer, id, "request_documents", "Please add the income statement.");

        var items = await ListAsync(citizen);
        Assert.Equal(["request_documents", "start_review", "submit"], items.Select(i => i.GetProperty("transition").GetString()));
        Assert.Equal("Please add the income statement.", items[0].GetProperty("note").GetString());
        Assert.Equal(3, await UnreadAsync(citizen));

        // The officer did the work, so they are not notified about it.
        Assert.Equal(0, await UnreadAsync(officer));
    }

    [Fact]
    public async Task The_applicants_own_later_actions_do_not_notify_them()
    {
        var agency = await TestUsers.TaxAgencyAsync(fixture);
        var citizen = await TestUsers.CitizenAsync(fixture);
        var officer = await TestUsers.StaffAsync(fixture, Roles.Officer, agency);
        var id = await SubmittedApplicationAsync(citizen);
        await Act(officer, id, "start_review");
        await Act(officer, id, "request_documents", "More please.");
        var before = await UnreadAsync(citizen);

        await Act(citizen, id, "provide_documents");

        Assert.Equal(before, await UnreadAsync(citizen));
    }

    [Fact]
    public async Task Notifications_can_be_marked_read_one_by_one_or_all_at_once()
    {
        var agency = await TestUsers.TaxAgencyAsync(fixture);
        var citizen = await TestUsers.CitizenAsync(fixture);
        var officer = await TestUsers.StaffAsync(fixture, Roles.Officer, agency);
        var id = await SubmittedApplicationAsync(citizen);
        await Act(officer, id, "start_review");
        var newest = (await ListAsync(citizen))[0].GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await citizen.Client.PostAsync($"{Notifications}/{newest}/read", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await citizen.Client.PostAsync($"{Notifications}/{newest}/read", null)).StatusCode);
        Assert.Equal(1, await UnreadAsync(citizen));
        Assert.Single(await ListAsync(citizen, "?unreadOnly=true"));

        Assert.Equal(HttpStatusCode.NoContent, (await citizen.Client.PostAsync($"{Notifications}/read-all", null)).StatusCode);
        Assert.Equal(0, await UnreadAsync(citizen));
        Assert.Empty(await ListAsync(citizen, "?unreadOnly=true"));
        Assert.Equal(2, (await ListAsync(citizen)).Count);
    }

    [Fact]
    public async Task Nobody_can_read_or_change_someone_elses_notifications()
    {
        var owner = await TestUsers.CitizenAsync(fixture);
        var stranger = await TestUsers.CitizenAsync(fixture);
        await SubmittedApplicationAsync(owner);
        var id = (await ListAsync(owner))[0].GetProperty("id").GetGuid();

        var response = await stranger.Client.PostAsync($"{Notifications}/{id}/read", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await ListAsync(stranger));
        Assert.Equal(1, await UnreadAsync(owner));

        // Marking everything read affects only the caller's own.
        await stranger.Client.PostAsync($"{Notifications}/read-all", null);
        Assert.Equal(1, await UnreadAsync(owner));
    }

    [Fact]
    public async Task Notifications_need_a_signed_in_user_and_a_sensible_page_size()
    {
        var anonymous = fixture.Factory.CreateClient();
        var citizen = await TestUsers.CitizenAsync(fixture);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Notifications)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"{Notifications}/unread-count")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await citizen.Client.GetAsync($"{Notifications}?pageSize=0")).StatusCode);
    }
}
