using OneGov.Domain.Applications;
using OneGov.Domain.Registry;

namespace OneGov.Domain.Tests;

public class ReferenceNumberTests
{
    [Fact]
    public void Formats_with_zero_padded_sequence() =>
        Assert.Equal("OG-TAXFILING-2026-000123", ReferenceNumber.Format("tax-filing", 2026, 123));

    [Theory]
    [InlineData("", 2026, 1)]
    [InlineData("---", 2026, 1)]
    [InlineData("TAX", 1999, 1)]
    [InlineData("TAX", 2026, 0)]
    public void Rejects_invalid_input(string code, int year, long sequence) =>
        Assert.ThrowsAny<ArgumentException>(() => ReferenceNumber.Format(code, year, sequence));
}

public class LocalizedTextTests
{
    [Fact]
    public void Falls_back_to_english_when_a_translation_is_missing()
    {
        var text = new LocalizedText("Tax filing", am: "የግብር ማስታወቂያ");

        Assert.Equal("የግብር ማስታወቂያ", text.Get("am"));
        Assert.Equal("Tax filing", text.Get("om"));
        Assert.Equal("Tax filing", text.Get("en"));
        Assert.Equal("Tax filing", text.Get("fr"));
    }

    [Fact]
    public void English_is_required() =>
        Assert.ThrowsAny<ArgumentException>(() => new LocalizedText(" "));
}

public class GovServiceTests
{
    [Fact]
    public void An_available_service_needs_a_workflow() =>
        Assert.Throws<ArgumentException>(() => new GovService(
            "X", Guid.NewGuid(), ServiceCategory.RevenueAndTax, new LocalizedText("n"), new LocalizedText("d"),
            ServiceStatus.Available, workflowCode: null));

    [Fact]
    public void Codes_are_normalised_to_upper_case()
    {
        var service = new GovService(
            " tax-filing ", Guid.NewGuid(), ServiceCategory.RevenueAndTax, new LocalizedText("n"), new LocalizedText("d"),
            ServiceStatus.Planned, workflowCode: null);

        Assert.Equal("TAX-FILING", service.Code);
    }
}
