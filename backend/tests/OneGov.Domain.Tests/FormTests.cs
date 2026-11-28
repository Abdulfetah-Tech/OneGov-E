using System.Text.Json;
using OneGov.Domain.Forms;

namespace OneGov.Domain.Tests;

public class FormValidatorTests
{
    private static readonly DateOnly Today = new(2026, 11, 27);

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static IReadOnlyDictionary<string, string> Tax(string json, bool complete) =>
        FormValidator.Validate(BuiltInForms.TaxFiling, Json(json), complete, Today);

    private static IReadOnlyDictionary<string, string> Id(string json, bool complete) =>
        FormValidator.Validate(BuiltInForms.IdRenewal, Json(json), complete, Today);

    [Fact]
    public void A_complete_valid_form_has_no_problems() =>
        Assert.Empty(Tax("""{"tin":"1234567890","taxYear":"2018","employmentIncome":120000.50,"hasForeignIncome":false,"notes":"ok"}""", complete: true));

    [Fact]
    public void A_draft_may_be_partly_filled_but_submitting_needs_every_required_field()
    {
        Assert.Empty(Tax("""{"taxYear":"2018"}""", complete: false));
        Assert.Empty(Tax("{}", complete: false));

        var problems = Tax("""{"taxYear":"2018"}""", complete: true);
        Assert.Equal("required", problems["tin"]);
        Assert.Equal("required", problems["employmentIncome"]);
        Assert.False(problems.ContainsKey("businessIncome"));
    }

    [Fact]
    public void Blank_text_counts_as_missing()
    {
        var problems = Tax("""{"tin":"   ","taxYear":"2018","employmentIncome":1}""", complete: true);

        Assert.Equal("required", problems["tin"]);
    }

    [Fact]
    public void Zero_and_false_are_real_answers_not_missing_ones() =>
        Assert.Empty(Tax("""{"tin":"1234567890","taxYear":"2017","employmentIncome":0,"hasForeignIncome":false}""", complete: true));

    [Fact]
    public void Fields_the_form_does_not_ask_for_are_refused_even_in_a_draft()
    {
        var problems = Tax("""{"taxYear":"2018","favouriteColour":"green"}""", complete: false);

        Assert.Equal("unknown_field", problems["favouriteColour"]);
    }

    [Theory]
    [InlineData("""{"tin":"123"}""", "pattern")]
    [InlineData("""{"tin":"12345678901"}""", "too_long")]
    [InlineData("""{"tin":"abcdefghij"}""", "pattern")]
    [InlineData("""{"tin":1234567890}""", "invalid_type")]
    [InlineData("""{"taxYear":"1999"}""", "invalid_choice")]
    [InlineData("""{"taxYear":2018}""", "invalid_choice")]
    [InlineData("""{"employmentIncome":-1}""", "below_min")]
    [InlineData("""{"employmentIncome":2000000000}""", "above_max")]
    [InlineData("""{"employmentIncome":"100"}""", "invalid_type")]
    [InlineData("""{"hasForeignIncome":"yes"}""", "invalid_type")]
    [InlineData("""{"notes":false}""", "invalid_type")]
    public void Values_that_break_a_rule_are_reported_with_the_reason(string json, string expected)
    {
        var problems = Tax(json, complete: false);

        Assert.Equal(expected, Assert.Single(problems).Value);
    }

    [Fact]
    public void Long_notes_are_refused()
    {
        var problems = Tax($$"""{"notes":"{{new string('a', 1001)}}"}""", complete: false);

        Assert.Equal("too_long", problems["notes"]);
    }

    [Fact]
    public void Dates_must_be_real_iso_dates_and_a_birth_date_must_be_in_the_past()
    {
        Assert.Empty(Id("""{"dateOfBirth":"1990-05-17"}""", complete: false));
        Assert.Equal("invalid_date", Id("""{"dateOfBirth":"17/05/1990"}""", complete: false)["dateOfBirth"]);
        Assert.Equal("invalid_date", Id("""{"dateOfBirth":"1990-02-30"}""", complete: false)["dateOfBirth"]);
        Assert.Equal("invalid_date", Id("""{"dateOfBirth":19900517}""", complete: false)["dateOfBirth"]);
        Assert.Equal("not_in_past", Id("""{"dateOfBirth":"2026-11-27"}""", complete: false)["dateOfBirth"]);
        Assert.Equal("not_in_past", Id("""{"dateOfBirth":"2030-01-01"}""", complete: false)["dateOfBirth"]);
    }

    private const string IdBase = "\"fullName\":\"Abebe\",\"nationalIdNumber\":\"1234567890\",\"dateOfBirth\":\"1990-05-17\"";

    [Fact]
    public void A_field_can_be_required_only_when_another_has_a_particular_answer()
    {
        Assert.Empty(Id("{" + IdBase + ",\"reason\":\"expired\"}", complete: true));
        Assert.Equal("required", Id("{" + IdBase + ",\"reason\":\"change_of_details\"}", complete: true)["changeDetails"]);
        Assert.Empty(Id("{" + IdBase + ",\"reason\":\"change_of_details\",\"changeDetails\":\"New surname\"}", complete: true));
    }

    [Fact]
    public void Phone_numbers_are_checked_when_given_but_not_required()
    {
        Assert.Empty(Id("""{"phone":"+251911223344"}""", complete: false));
        Assert.Equal("pattern", Id("""{"phone":"call me"}""", complete: false)["phone"]);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("42")]
    public void The_form_data_must_be_an_object(string json) =>
        Assert.Equal("invalid_payload", Tax(json, complete: false)["_"]);

    [Fact]
    public void A_pattern_that_takes_too_long_is_treated_as_not_matching()
    {
        var form = new FormDefinition(
            "X", 1, [new FormField("f", FieldType.Text, new("Field"), MaxLength: 100, Pattern: @"^(a+)+$")]);

        var problems = FormValidator.Validate(form, Json($$"""{"f":"{{new string('a', 60)}}!"}"""), complete: false, Today);

        Assert.Equal("pattern", problems["f"]);
    }
}

public class BuiltInFormsTests
{
    [Fact]
    public void Every_form_has_unique_field_keys_and_english_labels() =>
        Assert.All(BuiltInForms.All, form =>
        {
            Assert.Equal(form.Fields.Count, form.Fields.Select(f => f.Key).Distinct().Count());
            Assert.All(form.Fields, f => Assert.False(string.IsNullOrWhiteSpace(f.Label.En)));
        });

    [Fact]
    public void Choice_fields_have_options_and_other_fields_do_not() =>
        Assert.All(BuiltInForms.All.SelectMany(f => f.Fields), field =>
        {
            Assert.Equal(field.Type == FieldType.Choice, field.Options is { Count: > 0 });
            if (field.Options is not null)
            {
                Assert.Equal(field.Options.Count, field.Options.Select(o => o.Value).Distinct().Count());
            }
        });

    [Fact]
    public void Conditions_point_at_fields_that_exist() =>
        Assert.All(BuiltInForms.All, form =>
            Assert.All(form.Fields.Where(f => f.RequiredWhen is not null), field =>
                Assert.Contains(form.Fields, other => other.Key == field.RequiredWhen!.FieldKey)));

    [Fact]
    public void Patterns_compile() =>
        Assert.All(BuiltInForms.All.SelectMany(f => f.Fields).Where(f => f.Pattern is not null), field =>
            _ = new System.Text.RegularExpressions.Regex(field.Pattern!));

    [Fact]
    public void Forms_are_found_by_service_code_ignoring_case_and_by_version()
    {
        Assert.Same(BuiltInForms.TaxFiling, BuiltInForms.Latest("tax-filing"));
        Assert.Same(BuiltInForms.IdRenewal, BuiltInForms.Find("ID-RENEWAL", 1));
        Assert.Null(BuiltInForms.Find("ID-RENEWAL", 2));
        Assert.Null(BuiltInForms.Latest("PASSPORT-VISA"));
    }
}
