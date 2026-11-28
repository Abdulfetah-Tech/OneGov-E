using OneGov.Domain.Registry;

namespace OneGov.Domain.Forms;

/// <summary>
/// The forms for the sample services. SAMPLE rules, not official ones: the fields, limits and wording must be replaced with the real
/// requirements of the agencies, and the Amharic and Afaan Oromo wording reviewed by native speakers.
/// </summary>
public static class BuiltInForms
{
    public static FormDefinition TaxFiling { get; } = new(
        "TAX-FILING",
        1,
        [
            new FormField(
                "tin", FieldType.Text,
                new LocalizedText("Taxpayer Identification Number (TIN)", "የታክስ ከፋይ መለያ ቁጥር (TIN)", "Lakkoofsa Eenyummaa Gibira Kaffalaa (TIN)"),
                Required: true,
                Hint: new LocalizedText("10 digits", "10 አሃዞች", "Lakkoofsa 10"),
                MaxLength: 10, Pattern: @"^\d{10}$"),
            new FormField(
                "taxYear", FieldType.Choice,
                new LocalizedText("Tax year (Ethiopian calendar)", "የታክስ ዓመት (የኢትዮጵያ አቆጣጠር)", "Waggaa gibiraa (Keessa Ethiopia)"),
                Required: true,
                Options:
                [
                    new FormOption("2018", new LocalizedText("2018 E.C.", "2018 ዓ.ም.", "2018 A.L.H.")),
                    new FormOption("2017", new LocalizedText("2017 E.C.", "2017 ዓ.ም.", "2017 A.L.H.")),
                ]),
            new FormField(
                "employmentIncome", FieldType.Number,
                new LocalizedText("Employment income (Birr)", "የቅጥር ገቢ (ብር)", "Galii hojii (Birr)"),
                Required: true, Min: 0, Max: 1_000_000_000),
            new FormField(
                "businessIncome", FieldType.Number,
                new LocalizedText("Business income (Birr)", "የንግድ ገቢ (ብር)", "Galii daldalaa (Birr)"),
                Min: 0, Max: 1_000_000_000),
            new FormField(
                "otherIncome", FieldType.Number,
                new LocalizedText("Other income (Birr)", "ሌላ ገቢ (ብር)", "Galii biroo (Birr)"),
                Min: 0, Max: 1_000_000_000),
            new FormField(
                "hasForeignIncome", FieldType.Boolean,
                new LocalizedText("I received income from abroad", "ከውጭ ሀገር ገቢ አግኝቻለሁ", "Biyya alaa irraa galii argadheera")),
            new FormField(
                "notes", FieldType.LongText,
                new LocalizedText("Additional notes", "ተጨማሪ ማስታወሻዎች", "Yaadannoo dabalataa"),
                MaxLength: 1000),
        ]);

    public static FormDefinition IdRenewal { get; } = new(
        "ID-RENEWAL",
        1,
        [
            new FormField(
                "fullName", FieldType.Text,
                new LocalizedText("Full name", "ሙሉ ስም", "Maqaa guutuu"),
                Required: true, MaxLength: 100),
            new FormField(
                "nationalIdNumber", FieldType.Text,
                new LocalizedText("National ID number", "ብሔራዊ መታወቂያ ቁጥር", "Lakkoofsa ID biyyaalessaa"),
                Required: true, MaxLength: 20, Pattern: @"^\d{10,20}$"),
            new FormField(
                "dateOfBirth", FieldType.Date,
                new LocalizedText("Date of birth", "የትውልድ ቀን", "Guyyaa dhaloota"),
                Required: true, MustBeInPast: true),
            new FormField(
                "reason", FieldType.Choice,
                new LocalizedText("Reason for renewal", "የእድሳት ምክንያት", "Sababa haaromsaa"),
                Required: true,
                Options:
                [
                    new FormOption("expired", new LocalizedText("It has expired", "ጊዜው አልፏል", "Yeroon isaa darbeera")),
                    new FormOption("lost", new LocalizedText("It was lost or stolen", "ጠፍቷል ወይም ተሰርቋል", "Dhabameera ykn hatameera")),
                    new FormOption("damaged", new LocalizedText("It is damaged", "ተበላሽቷል", "Miidhameera")),
                    new FormOption("change_of_details", new LocalizedText("My details changed", "ዝርዝሮቼ ተቀይረዋል", "Odeeffannoon koo jijjiirameera")),
                ]),
            new FormField(
                "changeDetails", FieldType.LongText,
                new LocalizedText("What has changed?", "ምን ተቀይሯል?", "Maal jijjiirameera?"),
                MaxLength: 1000,
                RequiredWhen: new FieldCondition("reason", "change_of_details")),
            new FormField(
                "phone", FieldType.Text,
                new LocalizedText("Phone number", "ስልክ ቁጥር", "Lakkoofsa bilbilaa"),
                MaxLength: 16, Pattern: @"^\+?\d{9,15}$"),
        ]);

    public static IReadOnlyList<FormDefinition> All { get; } = [TaxFiling, IdRenewal];

    /// <summary>The newest form for a service, which new applications start on. Null when the service has no form yet.</summary>
    public static FormDefinition? Latest(string serviceCode) =>
        All.Where(f => string.Equals(f.ServiceCode, serviceCode, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.Version)
            .FirstOrDefault();

    public static FormDefinition? Find(string serviceCode, int version) =>
        All.FirstOrDefault(f => f.Version == version && string.Equals(f.ServiceCode, serviceCode, StringComparison.OrdinalIgnoreCase));
}
