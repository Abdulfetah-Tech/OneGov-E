using Microsoft.EntityFrameworkCore;
using OneGov.Domain.Registry;
using OneGov.Domain.Workflow;
using OneGov.Infrastructure.Persistence;

namespace OneGov.Infrastructure.Services;

/// <summary>
/// Synthetic registry data for development and tests. Agencies, rules and requirements are SAMPLES and do not
/// describe real Ethiopian government processes. Amharic/Afaan Oromo names below are DRAFTS pending native-speaker review;
/// everything else falls back to English.
/// </summary>
public static class DevelopmentDataSeeder
{
    public static async Task SeedAsync(OneGovDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Agencies.AnyAsync(cancellationToken))
        {
            return;
        }

        var revenue = new Agency("REV-SAMPLE", new LocalizedText("Revenue Authority (sample)"));
        var nationalId = new Agency("NID-SAMPLE", new LocalizedText("National ID Office (sample)"));
        var general = new Agency("GEN-SAMPLE", new LocalizedText("Sample agency for planned services"));
        db.Agencies.AddRange(revenue, nationalId, general);

        var tax = new GovService(
            "TAX-FILING",
            revenue.Id,
            ServiceCategory.RevenueAndTax,
            new LocalizedText("Digital tax filing (sample)", am: "የግብር ማስታወቂያ", om: "Gabaasa Gibiraa"),
            new LocalizedText("Prepare, submit and track a sample tax filing. Rules shown are placeholders, not official tax rules."),
            ServiceStatus.Available,
            BuiltInWorkflows.TaxFilingCode);
        tax.AddRequirement(RequirementKind.Eligibility, new LocalizedText("Registered taxpayer (sample rule)"), 1);
        tax.AddRequirement(RequirementKind.Document, new LocalizedText("Income statement (sample)"), 2);
        tax.AddRequirement(RequirementKind.Document, new LocalizedText("Supporting receipts (sample)"), 3);
        tax.AddRequirement(RequirementKind.Step, new LocalizedText("Complete the filing form"), 4);
        tax.AddRequirement(RequirementKind.Step, new LocalizedText("Review and submit"), 5);

        var idRenewal = new GovService(
            "ID-RENEWAL",
            nationalId.Id,
            ServiceCategory.IdentityAndCivilRegistration,
            new LocalizedText("National ID renewal (sample)", am: "የመታወቂያ እድሳት", om: "Haaromsa Eenyummaa"),
            new LocalizedText("Request and track a sample ID renewal. Identity verification is simulated, not a real national ID check."),
            ServiceStatus.Available,
            BuiltInWorkflows.IdRenewalCode);
        idRenewal.AddRequirement(RequirementKind.Eligibility, new LocalizedText("Existing ID holder (sample rule)"), 1);
        idRenewal.AddRequirement(RequirementKind.Document, new LocalizedText("Current ID reference (sample)"), 2);
        idRenewal.AddRequirement(RequirementKind.Document, new LocalizedText("Recent photograph (sample)"), 3);
        idRenewal.AddRequirement(RequirementKind.Step, new LocalizedText("Complete the renewal form"), 4);
        idRenewal.AddRequirement(RequirementKind.Step, new LocalizedText("Confirm identity (simulated)"), 5);

        db.Services.AddRange(tax, idRenewal);
        db.Services.AddRange(
            Planned("PASSPORT-VISA", general, ServiceCategory.Immigration, "Passport and visa services"),
            Planned("BUSINESS-REGISTRATION", general, ServiceCategory.BusinessRegistration, "Business registration"),
            Planned("LAND-REGISTRY", general, ServiceCategory.LandAndProperty, "Land and property records"),
            Planned("HEALTH-RECORDS", general, ServiceCategory.Healthcare, "Health records and certificates"),
            Planned("EDUCATION-CERTIFICATES", general, ServiceCategory.Education, "Education certificates"),
            Planned("DRIVING-LICENCE", general, ServiceCategory.Transportation, "Driving licence services"),
            Planned("LOCAL-PERMITS", general, ServiceCategory.LocalGovernment, "Local government permits"));

        await db.SaveChangesAsync(cancellationToken);
    }

    private static GovService Planned(string code, Agency agency, ServiceCategory category, string name) =>
        new(code, agency.Id, category, new LocalizedText(name), new LocalizedText($"{name} are planned and not yet available."),
            ServiceStatus.Planned, workflowCode: null);
}
