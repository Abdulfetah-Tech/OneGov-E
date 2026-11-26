namespace OneGov.Domain.Documents;

/// <summary>What the content of a file really is, decided from its first bytes, never from its name or the type the client claimed.</summary>
public sealed record DetectedFile(string ContentType, string Extension);

/// <summary>Rules for uploaded supporting documents. Pure, no I/O.</summary>
public static class DocumentRules
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MaxPerApplication = 10;
    public const int MaxNameLength = 100;

    private static readonly byte[] Pdf = "%PDF-"u8.ToArray();
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];

    /// <summary>Recognises PDF, PNG and JPEG by their signature. Anything else, including executables renamed to .pdf, is null.</summary>
    public static DetectedFile? Detect(ReadOnlySpan<byte> content)
    {
        if (content.StartsWith(Pdf))
        {
            return new DetectedFile("application/pdf", ".pdf");
        }

        if (content.StartsWith(Png))
        {
            return new DetectedFile("image/png", ".png");
        }

        if (content.StartsWith(Jpeg))
        {
            return new DetectedFile("image/jpeg", ".jpg");
        }

        return null;
    }

    /// <summary>
    /// A safe name to show and to download as: no folders, no control or reserved characters, bounded length, and an extension that
    /// matches the real content. Non-Latin names such as Amharic ones are kept.
    /// </summary>
    public static string DisplayName(string? clientName, string extension)
    {
        var name = clientName ?? string.Empty;

        // Browsers may send a full path. Keep only what follows the last separator, whichever kind it is.
        var lastSeparator = name.LastIndexOfAny(['/', '\\']);
        if (lastSeparator >= 0)
        {
            name = name[(lastSeparator + 1)..];
        }

        var dot = name.LastIndexOf('.');
        if (dot > 0)
        {
            name = name[..dot];
        }

        var cleaned = new string(name.Where(c => !char.IsControl(c) && "<>:\"|?*".IndexOf(c) < 0).ToArray()).Trim().Trim('.');
        while (cleaned.Contains("  ", StringComparison.Ordinal))
        {
            cleaned = cleaned.Replace("  ", " ", StringComparison.Ordinal);
        }

        if (cleaned.Length > MaxNameLength)
        {
            cleaned = cleaned[..MaxNameLength].TrimEnd();
        }

        return (cleaned.Length == 0 ? "document" : cleaned) + extension;
    }
}
