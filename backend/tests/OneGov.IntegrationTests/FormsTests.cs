using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OneGov.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class FormsTests(PostgresApiFixture fixture)
{
    private const string Applications = "/api/v1/applications";

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<(Guid Id, JsonElement Detail)> DraftAsync(TestSession citizen, string service)
    {
        var response = await citizen.Client.PostAsJsonAsync(Applications, new { serviceCode = service });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var detail = await Json(response);
        return (detail.GetProperty("id").GetGuid(), detail);
    }

    private static Task<HttpResponseMessage> SaveAsync(TestSession who, Guid id, object payload) =>
        who.Client.PutAsJsonAsync($"{Applications}/{id}/payload", new { payload });

    private static Task<HttpResponseMessage> SubmitAsync(TestSession who, Guid id) =>
        who.Client.PostAsJsonAsync($"{Applications}/{id}/transitions", new { transition = "submit" });

    private static async Task<Dictionary<string, string>> FieldErrorsAsync(HttpResponseMessage response)
    {
        var body = await Json(response);
        Assert.Equal("application.invalid_payload", body.GetProperty("type").GetString());
        return body.GetProperty("errors").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
    }

    [Fact]
    public async Task A_new_application_carries_its_forms_definition_in_all_three_languages()
    {
        var citizen = await TestUsers.CitizenAsync(fixture);

        var (_, detail) = await DraftAsync(citizen, "TAX-FILING");

        var form = detail.GetProperty("form");
        Assert.Equal("TAX-FILING", form.GetProperty("serviceCode").GetString());
        Assert.Equal(1, form.GetProperty("version").GetInt32());
        var tin = form.GetProperty("fields").EnumerateArray().Single(f => f.GetProperty("key").GetString() == "tin");
        Assert.Equal("text", tin.GetProperty("type").GetString());
        Assert.True(tin.GetProperty("required").GetBoolean());
        Assert.Equal("Taxpayer Identification Number (TIN)", tin.GetProperty("label").GetProperty("en").GetString());
        Assert.False(string.IsNullOrWhiteSpace(tin.GetProperty("label").GetProperty("am").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(tin.GetProperty("label").GetProperty("om").GetString()));
        var year = form.GetProperty("fields").EnumerateArray().Single(f => f.GetProperty("key").GetString() == "taxYear");
        Assert.Equal(2, year.GetProperty("options").GetArrayLength());
    }

    [Fact]
    public async Task A_draft_can_be_saved_partly_filled_but_wrong_values_are_refused_with_the_fields_named()
    {
        var citizen = await TestUsers.CitizenAsync(fixture);
        var (id, _) = await DraftAsync(citizen, "TAX-FILING");

        Assert.Equal(HttpStatusCode.OK, (await SaveAsync(citizen, id, new { taxYear = "2018" })).StatusCode);

        var bad = await SaveAsync(citizen, id, new { tin = "123", employmentIncome = -5, favouriteColour = "green" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var errors = await FieldErrorsAsync(bad);
        Assert.Equal("pattern", errors["tin"]);
        Assert.Equal("below_min", errors["employmentIncome"]);
        Assert.Equal("unknown_field", errors["favouriteColour"]);

        // The refused save changed nothing.
        var fetched = await Json(await citizen.Client.GetAsync($"{Applications}/{id}"));
        Assert.Equal("2018", fetched.GetProperty("payload").GetProperty("taxYear").GetString());
        Assert.False(fetched.GetProperty("payload").TryGetProperty("tin", out _));
    }

    [Fact]
    public async Task Submitting_an_incomplete_form_is_refused_and_the_application_stays_a_draft()
    {
        var citizen = await TestUsers.CitizenAsync(fixture);
        var (id, _) = await DraftAsync(citizen, "TAX-FILING");
        await SaveAsync(citizen, id, new { taxYear = "2018" });

        var refused = await SubmitAsync(citizen, id);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var errors = await FieldErrorsAsync(refused);
        Assert.Equal("required", errors["tin"]);
        Assert.Equal("required", errors["employmentIncome"]);
        Assert.Equal("draft", (await Json(await citizen.Client.GetAsync($"{Applications}/{id}"))).GetProperty("state").GetString());

        await TestForms.SaveValidTaxPayloadAsync(citizen, id);
        var accepted = await SubmitAsync(citizen, id);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("submitted", (await Json(accepted)).GetProperty("state").GetString());
    }

    [Fact]
    public async Task Sending_documents_back_to_review_also_needs_a_complete_form()
    {
        var agency = await TestUsers.TaxAgencyAsync(fixture);
        var citizen = await TestUsers.CitizenAsync(fixture);
        var officer = await TestUsers.StaffAsync(fixture, OneGov.Domain.Identity.Roles.Officer, agency);
        var (id, _) = await DraftAsync(citizen, "TAX-FILING");
        await TestForms.SaveValidTaxPayloadAsync(citizen, id);
        await SubmitAsync(citizen, id);
        await officer.Client.PostAsJsonAsync($"{Applications}/{id}/transitions", new { transition = "start_review" });
        await officer.Client.PostAsJsonAsync($"{Applications}/{id}/transitions", new { transition = "request_documents", note = "More please." });

        // The applicant may edit now, and blanking a required field stops the application going back.
        Assert.Equal(HttpStatusCode.OK, (await SaveAsync(citizen, id, new { tin = "" })).StatusCode);
        var refused = await citizen.Client.PostAsJsonAsync($"{Applications}/{id}/transitions", new { transition = "provide_documents" });

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("required", (await FieldErrorsAsync(refused))["tin"]);
    }

    [Fact]
    public async Task A_wrong_state_or_missing_permission_is_reported_as_that_and_not_as_a_form_problem()
    {
        var citizen = await TestUsers.CitizenAsync(fixture);
        var (id, _) = await DraftAsync(citizen, "TAX-FILING");

        var wrongState = await citizen.Client.PostAsJsonAsync($"{Applications}/{id}/transitions", new { transition = "provide_documents" });

        Assert.Equal(HttpStatusCode.Conflict, wrongState.StatusCode);
        Assert.Equal("workflow.invalid_state", (await Json(wrongState)).GetProperty("type").GetString());
    }

    [Fact]
    public async Task The_id_renewal_form_has_its_own_rules_including_a_conditional_field()
    {
        var citizen = await TestUsers.CitizenAsync(fixture);
        var (id, detail) = await DraftAsync(citizen, "ID-RENEWAL");
        Assert.Equal("ID-RENEWAL", detail.GetProperty("form").GetProperty("serviceCode").GetString());

        var future = await SaveAsync(citizen, id, new { dateOfBirth = "2031-01-01" });
        Assert.Equal("not_in_past", (await FieldErrorsAsync(future))["dateOfBirth"]);

        await SaveAsync(citizen, id, new
        {
            fullName = "Abebe Bekele",
            nationalIdNumber = "1234567890123456",
            dateOfBirth = "1990-05-17",
            reason = "change_of_details",
        });
        var missing = await SubmitAsync(citizen, id);
        Assert.Equal("required", (await FieldErrorsAsync(missing))["changeDetails"]);

        await SaveAsync(citizen, id, new
        {
            fullName = "Abebe Bekele",
            nationalIdNumber = "1234567890123456",
            dateOfBirth = "1990-05-17",
            reason = "change_of_details",
            changeDetails = "Changed surname after marriage.",
        });
        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync(citizen, id)).StatusCode);
    }

    [Fact]
    public async Task A_locked_application_answers_not_editable_before_any_field_problem()
    {
        var citizen = await TestUsers.CitizenAsync(fixture);
        var (id, _) = await DraftAsync(citizen, "TAX-FILING");
        await TestForms.SaveValidTaxPayloadAsync(citizen, id);
        await SubmitAsync(citizen, id);

        var response = await SaveAsync(citizen, id, new { notAField = 1 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application.not_editable", (await Json(response)).GetProperty("type").GetString());
    }
}
