namespace OneGov.Domain.Registry;

/// <summary>
/// Text in English, Amharic and Afaan Oromo. Empty Amharic/Oromo values fall back to English, so unreviewed or
/// missing translations never show blank content. Official wording must be reviewed by native speakers.
/// </summary>
public sealed class LocalizedText
{
    private LocalizedText()
    {
    }

    public LocalizedText(string en, string? am = null, string? om = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(en);
        En = en;
        Am = am ?? string.Empty;
        Om = om ?? string.Empty;
    }

    public string En { get; private set; } = string.Empty;

    public string Am { get; private set; } = string.Empty;

    public string Om { get; private set; } = string.Empty;

    public string Get(string language) => language switch
    {
        "am" when !string.IsNullOrWhiteSpace(Am) => Am,
        "om" when !string.IsNullOrWhiteSpace(Om) => Om,
        _ => En,
    };
}
