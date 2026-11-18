using System.Globalization;

namespace OneGov.Domain.Applications;

/// <summary>Human-readable application reference, e.g. <c>OG-TAXFILING-2026-000123</c>.</summary>
public static class ReferenceNumber
{
    public static string Format(string serviceCode, int year, long sequence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceCode);
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 2000);
        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);

        var code = new string(serviceCode.Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();
        if (code.Length == 0)
        {
            throw new ArgumentException("Service code must contain letters or digits.", nameof(serviceCode));
        }

        return string.Create(CultureInfo.InvariantCulture, $"OG-{code}-{year}-{sequence:D6}");
    }
}
