using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OneGov.Domain.Documents;
using OneGov.Domain.Identity;

namespace OneGov.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class DocumentsTests(PostgresApiFixture fixture)
{
    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<string> ProblemType(HttpResponseMessage response) =>
        (await Json(response)).GetProperty("type").GetString()!;

    private static byte[] Pdf(int size = 200)
    {
        var bytes = new byte[size];
        Encoding.ASCII.GetBytes("%PDF-1.4\n").CopyTo(bytes, 0);
        Array.Fill(bytes, (byte)'x', 9, size - 9);
        return bytes;
    }

    private static string DocumentsUrl(Guid applicationId) => $"/api/v1/applications/{applicationId}/documents";

    private static Task<HttpResponseMessage> UploadAsync(
        TestSession who, Guid applicationId, byte[] content, string fileName = "income.pdf", string contentType = "application/pdf")
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent { { file, "file", fileName } };
        return who.Client.PostAsync(DocumentsUrl(applicationId), form);
    }

    private static async Task<Guid> DraftAsync(TestSession citizen)
    {
        var response = await citizen.Client.PostAsJsonAsync("/api/v1/applications", new { serviceCode = "TAX-FILING" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Json(response)).GetProperty("id").GetGuid();
    }

    private static async Task ActAsync(TestSession who, Guid id, string transition, string? note = null)
    {
        if (transition == "submit")
        {
            await TestForms.SaveValidTaxPayloadAsync(who, id);
        }

        var response = await who.Client.PostAsJsonAsync($"/api/v1/applications/{id}/transitions", new { transition, note });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<Guid> UploadOkAsync(TestSession who, Guid applicationId, byte[]? content = null, string fileName = "income.pdf")
    {
        var response = await UploadAsync(who, applicationId, content ?? Pdf(), fileName);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Json(response)).GetProperty("id").GetGuid();
    }

    private static async Task<List<JsonElement>> ListAsync(TestSession who, Guid applicationId)
    {
        var response = await who.Client.GetAsync(DocumentsUrl(applicationId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await Json(response)).EnumerateArray().ToList();
    }

    private int StoredFiles() =>
        Directory.Exists(fixture.DocumentsRoot) ? Directory.GetFiles(fixture.DocumentsRoot, "*", SearchOption.AllDirectories).Length : 0;

    [Fact]
    public async Task The_applicant_uploads_a_pdf_and_can_download_exactly_the_same_bytes()
    {
        var citizen = await TestUsers.CitizenAsync(fixture);
        var id = await DraftAsync(citizen);
        var content = Pdf(1234);

        var response = await UploadAsync(citizen, id, content, "Income statement.pdf");
        var dto = await Json(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Income statement.pdf", dto.GetProperty("fileName").GetString());
        Assert.Equal("application/pdf", dto.GetProperty("contentType").GetString());
        Assert.Equal(1234, dto.GetProperty("sizeBytes").GetInt64());
        Assert.Single(await ListAsync(citizen, id));

        var download = await citizen.Client.GetAsync($"{DocumentsUrl(id)}/{dto.GetProperty("id").GetGuid()}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(content, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Contains("nosniff", download.Headers.GetValues("X-Content-Type-Options"));
    }

    [Fact]
    public async Task Pngs_and_jpegs_are_accepted_and_the_client_claimed_type_is_ignored()
    {
        var citizen = await TestUsers.CitizenAsync(fixture);
        var id = await DraftAsync(citizen);
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01];

        var response = await UploadAsync(citizen, id, png, "scan.bin", "application/x-msdownload");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await Json(response);
        Assert.Equal("image/png", dto.GetProperty("contentType").GetString());
        Assert.Equal("scan.png", dto.GetProperty("fileName").GetString());
    }

    [Fact]
    public async Task Names_are_sanitised_and_nothing_user_supplied_reaches_the_disk_path()
    {
        var citizen = await TestUsers.CitizenAsync(fixture);
        var id = await DraftAsync(citizen);

        var response = await UploadAsync(citizen, id, Pdf(), "../../etc/evil.pdf");

        Assert.Equal("evil.pdf", (await Json(response)).GetProperty("fileName").GetString());
        var stored = Directory.GetFiles(fixture.DocumentsRoot, "*", SearchOption.AllDirectories);
        Assert.All(stored, path => Assert.Matches(new Regex("^[0-9a-f]{32}$"), Path.GetFileName(path)));
    }

    [Theory]
    [InlineData("MZ\u0090\u0000 a program renamed to pdf")]
    [InlineData("plain text, not a document")]
    [InlineData("")]
    public async Task Files_that_are_not_pdf_png_or_jpeg_are_refused_whatever_they_are_called(string content)
    {
        var citizen = await TestUsers.CitizenAsync(fixture);
        var id = await DraftAsync(citizen);
        var before = StoredFiles();

        var response = await UploadAsync(citizen, id, Encoding.Latin1.GetBytes(content), "income.pdf");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("document.invalid_file", await ProblemType(response));
        Assert.Equal(before, StoredFiles());
    }

    [Fact]
    public async Task Files_over_the_size_limit_are_refused()
    {
        var citizen = await TestUsers.CitizenAsync(fixture);
        var id = await DraftAsync(citizen);

        var response = await UploadAsync(citizen, id, Pdf(DocumentRules.MaxBytes + 1));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("document.too_large", await ProblemType(response));
    }

    [Fact]
    public async Task An_application_holds_at_most_ten_documents()
    {
        var citizen = await TestUsers.CitizenAsync(fixture);
        var id = await DraftAsync(citizen);
        for (var i = 0; i < DocumentRules.MaxPerApplication; i++)
        {
            await UploadOkAsync(citizen, id, Pdf(50 + i));
        }

        var eleventh = await UploadAsync(citizen, id, Pdf());

        Assert.Equal(HttpStatusCode.Conflict, eleventh.StatusCode);
        Assert.Equal("document.limit_reached", await ProblemType(eleventh));
    }

    [Fact]
    public async Task Other_citizens_cannot_see_add_download_or_remove_documents()
    {
        var owner = await TestUsers.CitizenAsync(fixture);
        var stranger = await TestUsers.CitizenAsync(fixture);
        var id = await DraftAsync(owner);
        var documentId = await UploadOkAsync(owner, id);

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.GetAsync(DocumentsUrl(id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await UploadAsync(stranger, id, Pdf())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.GetAsync($"{DocumentsUrl(id)}/{documentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.DeleteAsync($"{DocumentsUrl(id)}/{documentId}")).StatusCode);
        Assert.Single(await ListAsync(owner, id));
    }

    [Fact]
    public async Task Anonymous_callers_are_turned_away()
    {
        var anonymous = fixture.Factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(DocumentsUrl(Guid.NewGuid()))).StatusCode);
    }

    [Fact]
    public async Task Staff_of_the_agency_can_read_the_documents_but_not_change_them_and_other_agencies_see_nothing()
    {
        var agency = await TestUsers.TaxAgencyAsync(fixture);
        var citizen = await TestUsers.CitizenAsync(fixture);
        var officer = await TestUsers.StaffAsync(fixture, Roles.Officer, agency);
        var otherOfficer = await TestUsers.StaffAsync(fixture, Roles.Officer, await TestUsers.OtherAgencyAsync(fixture));
        var id = await DraftAsync(citizen);
        var content = Pdf(777);
        var documentId = await UploadOkAsync(citizen, id, content);
        await ActAsync(citizen, id, "submit");

        var download = await officer.Client.GetAsync($"{DocumentsUrl(id)}/{documentId}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(content, await download.Content.ReadAsByteArrayAsync());
        Assert.Single(await ListAsync(officer, id));

        var add = await UploadAsync(officer, id, Pdf());
        Assert.Equal(HttpStatusCode.Forbidden, add.StatusCode);
        Assert.Equal("application.not_applicant", await ProblemType(add));
        Assert.Equal(HttpStatusCode.Forbidden, (await officer.Client.DeleteAsync($"{DocumentsUrl(id)}/{documentId}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await otherOfficer.Client.GetAsync(DocumentsUrl(id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherOfficer.Client.GetAsync($"{DocumentsUrl(id)}/{documentId}")).StatusCode);
    }

    [Fact]
    public async Task Documents_can_only_be_changed_while_the_application_is_open_for_changes()
    {
        var agency = await TestUsers.TaxAgencyAsync(fixture);
        var citizen = await TestUsers.CitizenAsync(fixture);
        var officer = await TestUsers.StaffAsync(fixture, Roles.Officer, agency);
        var id = await DraftAsync(citizen);
        var documentId = await UploadOkAsync(citizen, id);

        // Once submitted and under review, the applicant can no longer add or remove files.
        await ActAsync(citizen, id, "submit");
        var locked = await UploadAsync(citizen, id, Pdf());
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);
        Assert.Equal("application.not_editable", await ProblemType(locked));
        Assert.Equal(HttpStatusCode.Conflict, (await citizen.Client.DeleteAsync($"{DocumentsUrl(id)}/{documentId}")).StatusCode);

        // When staff ask for documents, uploading opens up again.
        await ActAsync(officer, id, "start_review");
        await ActAsync(officer, id, "request_documents", "Please add the income statement.");
        Assert.Equal(HttpStatusCode.Created, (await UploadAsync(citizen, id, Pdf(300), "income-statement.pdf")).StatusCode);
        Assert.Equal(2, (await ListAsync(citizen, id)).Count);
    }

    [Fact]
    public async Task Removing_a_document_hides_it_and_deletes_the_stored_file()
    {
        var citizen = await TestUsers.CitizenAsync(fixture);
        var id = await DraftAsync(citizen);
        var documentId = await UploadOkAsync(citizen, id);
        var filesWithIt = StoredFiles();

        var removed = await citizen.Client.DeleteAsync($"{DocumentsUrl(id)}/{documentId}");

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Empty(await ListAsync(citizen, id));
        Assert.Equal(HttpStatusCode.NotFound, (await citizen.Client.GetAsync($"{DocumentsUrl(id)}/{documentId}")).StatusCode);
        Assert.Equal(filesWithIt - 1, StoredFiles());
        Assert.Equal(HttpStatusCode.NotFound, (await citizen.Client.DeleteAsync($"{DocumentsUrl(id)}/{documentId}")).StatusCode);
    }
}
