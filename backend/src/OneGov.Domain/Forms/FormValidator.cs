using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OneGov.Domain.Forms;

/// <summary>
/// Checks form data against its definition. The server is the authority: the web form can help people early, but it is not trusted.
/// Pure, no I/O. Returns the first problem per field as a stable code, or an empty result when the data is acceptable.
/// </summary>
public static class FormValidator
{
    private static readonly TimeSpan PatternTimeout = TimeSpan.FromMilliseconds(100);

    /// <param name="complete">
    /// False while saving a draft: values that are present must be valid, but missing ones are fine.
    /// True when submitting: required fields must also be filled.
    /// </param>
    public static IReadOnlyDictionary<string, string> Validate(
        FormDefinition form, JsonElement payload, bool complete, DateOnly today)
    {
        var errors = new Dictionary<string, string>(StringComparer.Ordinal);
        if (payload.ValueKind != JsonValueKind.Object)
        {
            errors["_"] = "invalid_payload";
            return errors;
        }

        var known = form.Fields.Select(f => f.Key).ToHashSet(StringComparer.Ordinal);
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in payload.EnumerateObject())
        {
            if (known.Contains(property.Name))
            {
                values[property.Name] = property.Value;
            }
            else
            {
                errors[property.Name] = "unknown_field";
            }
        }

        foreach (var field in form.Fields)
        {
            values.TryGetValue(field.Key, out var value);
            if (IsMissing(value))
            {
                if (complete && IsRequired(field, values))
                {
                    errors[field.Key] = "required";
                }

                continue;
            }

            var problem = Check(field, value, today);
            if (problem is not null)
            {
                errors[field.Key] = problem;
            }
        }

        return errors;
    }

    private static bool IsMissing(JsonElement value) =>
        value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
        || (value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString()));

    private static bool IsRequired(FormField field, Dictionary<string, JsonElement> values) =>
        field.Required
        || (field.RequiredWhen is { } condition
            && values.TryGetValue(condition.FieldKey, out var other)
            && other.ValueKind == JsonValueKind.String
            && other.GetString() == condition.EqualsValue);

    private static string? Check(FormField field, JsonElement value, DateOnly today) =>
        field.Type switch
        {
            FieldType.Text or FieldType.LongText => CheckText(field, value),
            FieldType.Number => CheckNumber(field, value),
            FieldType.Date => CheckDate(field, value, today),
            FieldType.Choice => CheckChoice(field, value),
            FieldType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False ? null : "invalid_type",
            _ => "invalid_type",
        };

    private static string? CheckText(FormField field, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            return "invalid_type";
        }

        var text = value.GetString()!;
        if (text.Length > field.MaxLength)
        {
            return "too_long";
        }

        if (field.Pattern is not null)
        {
            try
            {
                if (!Regex.IsMatch(text, field.Pattern, RegexOptions.CultureInvariant, PatternTimeout))
                {
                    return "pattern";
                }
            }
            catch (RegexMatchTimeoutException)
            {
                return "pattern";
            }
        }

        return null;
    }

    private static string? CheckNumber(FormField field, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var number))
        {
            return "invalid_type";
        }

        if (field.Min is { } min && number < min)
        {
            return "below_min";
        }

        return field.Max is { } max && number > max ? "above_max" : null;
    }

    private static string? CheckDate(FormField field, JsonElement value, DateOnly today)
    {
        if (value.ValueKind != JsonValueKind.String
            || !DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return "invalid_date";
        }

        return field.MustBeInPast && date >= today ? "not_in_past" : null;
    }

    private static string? CheckChoice(FormField field, JsonElement value) =>
        value.ValueKind == JsonValueKind.String
        && field.Options is { } options
        && options.Any(o => o.Value == value.GetString())
            ? null
            : "invalid_choice";
}
