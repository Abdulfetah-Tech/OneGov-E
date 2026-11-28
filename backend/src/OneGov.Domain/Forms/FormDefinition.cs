using OneGov.Domain.Registry;

namespace OneGov.Domain.Forms;

public enum FieldType
{
    Text,
    LongText,
    Number,

    /// <summary>A calendar date written as yyyy-MM-dd (Gregorian). The web app shows it in the person's calendar.</summary>
    Date,
    Choice,
    Boolean,
}

public sealed record FormOption(string Value, LocalizedText Label);

/// <summary>A field is required only when another field has this exact text value.</summary>
public sealed record FieldCondition(string FieldKey, string EqualsValue);

public sealed record FormField(
    string Key,
    FieldType Type,
    LocalizedText Label,
    bool Required = false,
    LocalizedText? Hint = null,
    int MaxLength = 200,
    string? Pattern = null,
    decimal? Min = null,
    decimal? Max = null,
    bool MustBeInPast = false,
    IReadOnlyList<FormOption>? Options = null,
    FieldCondition? RequiredWhen = null);

/// <summary>What an application for a service asks the applicant. The same definition drives the web form and the server's checks.</summary>
public sealed record FormDefinition(string ServiceCode, int Version, IReadOnlyList<FormField> Fields);
