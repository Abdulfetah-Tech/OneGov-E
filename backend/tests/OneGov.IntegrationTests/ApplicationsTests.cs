using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using OneGov.Domain.Identity;

namespace OneGov.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ApplicationsTests(PostgresApiFixture fixture)
{
    private const string Applications = "/api/v1/applications";

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<string> ProblemType(HttpResponseMessage response) =>
        (await Json(response)).GetProperty("type").GetString()!;

    private static async Task<Guid> CreateDraftAsync(TestSession citizen, string service = "TAX-FILING")
    {
        var response = await citizen.Client.PostAsJsonAsync(Applications, new { serviceCode = service });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Json(response)).GetProperty("id").GetGuid();
    }

    private static async Task<HttpResponseMessage> TransitionAsync(TestSession who, Guid id, string transition, string? note = null)
    {
        // Submitting needs a complete form, so these tests fill in a valid one first.
        if (transition == "submit")
        {
            await TestForms.SaveValidTaxPayloadAsync(who, id);
        }

        return await who.Client.PostAsJsonAsync($"{Applications}/{id}/transitions", new { transition, note });
    }

    private static async Task<JsonElement> OkAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await Json(response);
    }

    private static IEnumerable<string?> Strings(JsonElement array) => array.EnumerateArray().Select(e => e.GetString());

    [Fact]
    public async Task A_citizen_starts_a_draft_for_an_available_service()
    {
        var citizen = await TestUsers.CitizenAsync(fixture);

        var response = await citizen.Client.PostAsJsonAsync(Applications, new { serviceCode = "tax-filing" });
        var draft = await Json(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("draft", draft.GetProperty("state").GetString());
        Assert.Equal("TAX-FILING", draft.GetProperty("serviceCode").GetString());
        Assert.Equal(JsonValueKind.Null, draft.GetProperty("referenceNumber").ValueKind);
        Assert.True(draft.GetProperty("isEditable").GetBoolean());
        Assert.Contains("submit", Strings(draft.GetProperty("availableTransitions")));
        Assert.Contains("withdraw", Strings(draft.GetProperty("availableTransitions")));
    }

    [Fact]
    public async Task A_service_that_is_only_planned_or_does_not_exist_cannot_be_applied_for()
    {
        var citizen = await TestUsers.CitizenAsync(fixture);

        var planned = await citizen.Client.PostAsJsonAsync(Applications, new { serviceCode = "PASSPORT-VISA" });
        var missing = await citizen.Client.PostAsJsonAsync(Applications, new { serviceCode = "NO-SUCH-SERVICE" });

        Assert.Equal(HttpStatusCode.Conflict, planned.StatusCode);
        Assert.Equal("application.service_unavailable", await ProblemType(planned));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Anonymous_callers_and_staff_cannot_start_applications()
    {
        var anonymous = await fixture.Factory.CreateClient().PostAsJsonAsync(Applications, new { serviceCode = "TAX-FILING" });
        var officer = await TestUsers.StaffAsync(fixture, Roles.Officer, await TestUsers.TaxAgencyAsync(fixture));
        var asStaff = await officer.Client.PostAsJsonAsync(Applications, new { serviceCode = "TAX-FILING" });

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, asStaff.StatusCode);
    }

    [Fact]
    public async Task The_applicant_saves_form_data_but_only_objects_are_accepted()
    {
        var citizen = await TestUsers.CitizenAsync(fixture);
        var id = await CreateDraftAsync(citizen);

        var saved = await OkAsync(await citizen.Client.PutAsJsonAsync($"{Applications}/{id}/payload", new { payload = new { employmentIncome = 1200, taxYear = "2018" } }));
        var notObject = await citizen.Client.PutAsJsonAsync($"{Applications}/{id}/payload", new { payload = new[] { 1, 2 } });
        var fetched = await OkAsync(await citizen.Client.GetAsync($"{Applications}/{id}"));

        Assert.Equal(1200, saved.GetProperty("payload").GetProperty("employmentIncome").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, notObject.StatusCode);
        Assert.Equal("application.invalid_payload", await ProblemType(notObject));
        Assert.Equal("2018", fetched.GetProperty("payload").GetProperty("taxYear").GetString());
    }

    [Fact]
    public async Task Other_citizens_cannot_see_or_change_an_application()
    {
        var owner = await TestUsers.CitizenAsync(fixture);
        var stranger = await TestUsers.CitizenAsync(fixture);
        var id = await CreateDraftAsync(owner);

        var read = await stranger.Client.GetAsync($"{Applications}/{id}");
        var write = await stranger.Client.PutAsJsonAsync($"{Applications}/{id}/payload", new { payload = new { x = 1 } });
        var act = await TransitionAsync(stranger, id, "withdraw");
        var list = await OkAsync(await stranger.Client.GetAsync(Applications));

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, act.StatusCode);
        Assert.Equal(0, list.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task A_tax_filing_goes_from_draft_to_approved_with_every_step_recorded()
    {
        var agency = await TestUsers.TaxAgencyAsync(fixture);
        var citizen = await TestUsers.CitizenAsync(fixture);
        var officer = await TestUsers.StaffAsync(fixture, Roles.Officer, agency);
        var approver = await TestUsers.StaffAsync(fixture, Roles.Approver, agency);
        var id = await CreateDraftAsync(citizen);

        // The citizen submits and receives a reference number. After that the form is locked.
        var submitted = await OkAsync(await TransitionAsync(citizen, id, "submit"));
        Assert.Equal("submitted", submitted.GetProperty("state").GetString());
        Assert.Matches(new Regex(@"^OG-TAXFILING-\d{4}-\d{6}$"), submitted.GetProperty("referenceNumber").GetString()!);
        Assert.False(submitted.GetProperty("isEditable").GetBoolean());
        var locked = await citizen.Client.PutAsJsonAsync($"{Applications}/{id}/payload", new { payload = new { x = 1 } });
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);
        Assert.Equal("application.not_editable", await ProblemType(locked));

        // The officer of that agency finds it in the queue and starts the review.
        var queue = await OkAsync(await officer.Client.GetAsync($"{Applications}?state=submitted&pageSize=100"));
        Assert.Contains(id, queue.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()));
        Assert.Contains("start_review", Strings((await OkAsync(await officer.Client.GetAsync($"{Applications}/{id}"))).GetProperty("availableTransitions")));
        await OkAsync(await TransitionAsync(officer, id, "start_review"));

        // Asking for documents needs a note. The citizen then supplies them, which returns it to review.
        var noNote = await TransitionAsync(officer, id, "request_documents");
        Assert.Equal(HttpStatusCode.BadRequest, noNote.StatusCode);
        Assert.Equal("workflow.note_required", await ProblemType(noNote));
        var requested = await OkAsync(await TransitionAsync(officer, id, "request_documents", "Please add the income statement."));
        Assert.Equal("documents_requested", requested.GetProperty("state").GetString());
        await OkAsync(await TransitionAsync(citizen, id, "provide_documents"));

        // Reviewing and deciding are separate jobs: the officer cannot approve, the approver can.
        var officerApproves = await TransitionAsync(officer, id, "approve");
        Assert.Equal(HttpStatusCode.Forbidden, officerApproves.StatusCode);
        Assert.Equal("workflow.forbidden", await ProblemType(officerApproves));
        var approved = await OkAsync(await TransitionAsync(approver, id, "approve", "Complete."));
        Assert.Equal("approved", approved.GetProperty("state").GetString());

        // Nothing more can happen to an approved application, and the history tells the whole story.
        var late = await TransitionAsync(citizen, id, "withdraw");
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        Assert.Equal("workflow.invalid_state", await ProblemType(late));

        var history = (await OkAsync(await citizen.Client.GetAsync($"{Applications}/{id}"))).GetProperty("history").EnumerateArray().ToList();
        Assert.Equal(
            ["submit", "start_review", "request_documents", "provide_documents", "approve"],
            history.Select(h => h.GetProperty("transition").GetString()));
        Assert.Equal(approver.UserId, history[^1].GetProperty("actorId").GetGuid());
        Assert.Equal("Complete.", history[^1].GetProperty("note").GetString());
    }

    [Fact]
    public async Task A_citizen_cannot_use_staff_transitions()
    {
        var citizen = await TestUsers.CitizenAsync(fixture);
        var id = await CreateDraftAsync(citizen);
        await OkAsync(await TransitionAsync(citizen, id, "submit"));

        var response = await TransitionAsync(citizen, id, "start_review");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("workflow.forbidden", await ProblemType(response));
    }

    [Fact]
    public async Task Staff_of_another_agency_cannot_see_the_application()
    {
        var citizen = await TestUsers.CitizenAsync(fixture);
        var otherOfficer = await TestUsers.StaffAsync(fixture, Roles.Officer, await TestUsers.OtherAgencyAsync(fixture));
        var id = await CreateDraftAsync(citizen);
        await OkAsync(await TransitionAsync(citizen, id, "submit"));

        var read = await otherOfficer.Client.GetAsync($"{Applications}/{id}");
        var act = await TransitionAsync(otherOfficer, id, "start_review");
        var queue = await OkAsync(await otherOfficer.Client.GetAsync($"{Applications}?pageSize=100"));

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, act.StatusCode);
        Assert.DoesNotContain(id, queue.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task Staff_who_signed_in_without_a_second_factor_see_nothing()
    {
        var agency = await TestUsers.TaxAgencyAsync(fixture);
        var citizen = await TestUsers.CitizenAsync(fixture);
        var officerNoMfa = await TestUsers.StaffAsync(fixture, Roles.Officer, agency, withMfa: false);
        var id = await CreateDraftAsync(citizen);
        await OkAsync(await TransitionAsync(citizen, id, "submit"));

        var read = await officerNoMfa.Client.GetAsync($"{Applications}/{id}");
        var queue = await OkAsync(await officerNoMfa.Client.GetAsync(Applications));

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(0, queue.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Staff_cannot_work_on_their_own_application()
    {
        var agency = await TestUsers.TaxAgencyAsync(fixture);
        var officerWhoApplies = await TestUsers.StaffAsync(fixture, Roles.Officer, agency, alsoCitizen: true);
        var id = await CreateDraftAsync(officerWhoApplies);
        await OkAsync(await TransitionAsync(officerWhoApplies, id, "submit"));

        var response = await TransitionAsync(officerWhoApplies, id, "start_review");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application.conflict_of_interest", await ProblemType(response));
    }

    [Fact]
    public async Task Staff_who_are_also_citizens_cannot_act_as_the_applicant_on_someone_elses_application()
    {
        var agency = await TestUsers.TaxAgencyAsync(fixture);
        var citizen = await TestUsers.CitizenAsync(fixture);
        var staffAndCitizen = await TestUsers.StaffAsync(fixture, Roles.Officer, agency, alsoCitizen: true);
        var id = await CreateDraftAsync(citizen);
        await OkAsync(await TransitionAsync(citizen, id, "submit"));

        var withdraw = await TransitionAsync(staffAndCitizen, id, "withdraw");

        Assert.Equal(HttpStatusCode.Forbidden, withdraw.StatusCode);
        Assert.Equal("application.not_applicant", await ProblemType(withdraw));
    }

    [Fact]
    public async Task Each_submitted_application_gets_its_own_reference_number()
    {
        var citizen = await TestUsers.CitizenAsync(fixture);
        var first = await CreateDraftAsync(citizen);
        var second = await CreateDraftAsync(citizen);

        var a = await OkAsync(await TransitionAsync(citizen, first, "submit"));
        var b = await OkAsync(await TransitionAsync(citizen, second, "submit"));

        Assert.NotEqual(a.GetProperty("referenceNumber").GetString(), b.GetProperty("referenceNumber").GetString());
    }

    [Fact]
    public async Task Listing_is_paged_and_unknown_transitions_are_rejected()
    {
        var citizen = await TestUsers.CitizenAsync(fixture);
        var id = await CreateDraftAsync(citizen);
        await CreateDraftAsync(citizen);

        var page = await OkAsync(await citizen.Client.GetAsync($"{Applications}?pageSize=1&page=2"));
        var badPage = await citizen.Client.GetAsync($"{Applications}?pageSize=0");
        var unknown = await TransitionAsync(citizen, id, "teleport");

        Assert.Equal(1, page.GetProperty("items").GetArrayLength());
        Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, badPage.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal("workflow.unknown_transition", await ProblemType(unknown));
    }
}
