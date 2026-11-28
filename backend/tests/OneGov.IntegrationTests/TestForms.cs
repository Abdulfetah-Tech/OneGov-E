using System.Net;
using System.Net.Http.Json;

namespace OneGov.IntegrationTests;

/// <summary>Valid form data for the sample services, so tests that are not about forms can still submit.</summary>
internal static class TestForms
{
    public static object ValidTaxPayload => new { tin = "1234567890", taxYear = "2018", employmentIncome = 120000 };

    public static object ValidIdRenewalPayload => new
    {
        fullName = "Abebe Bekele",
        nationalIdNumber = "1234567890123456",
        dateOfBirth = "1990-05-17",
        reason = "expired",
    };

    /// <summary>Saves a valid tax filing form to the application, as the applicant.</summary>
    public static async Task SaveValidTaxPayloadAsync(TestSession applicant, Guid applicationId)
    {
        var response = await applicant.Client.PutAsJsonAsync(
            $"/api/v1/applications/{applicationId}/payload", new { payload = ValidTaxPayload });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
