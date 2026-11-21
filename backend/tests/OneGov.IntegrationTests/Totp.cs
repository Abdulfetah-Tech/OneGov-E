using System.Globalization;
using System.Security.Cryptography;

namespace OneGov.IntegrationTests;

/// <summary>RFC 6238 time-based one-time passwords (SHA-1), so tests can act as an authenticator app.</summary>
internal static class Totp
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Compute(string base32Secret, DateTimeOffset at, int digits = 6, int stepSeconds = 30)
    {
        var key = DecodeBase32(base32Secret);
        var counter = at.ToUnixTimeSeconds() / stepSeconds;

        var message = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(message);
        }

        var hash = HMACSHA1.HashData(key, message);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
            | (hash[offset + 1] << 16)
            | (hash[offset + 2] << 8)
            | hash[offset + 3];

        var modulus = (int)Math.Pow(10, digits);
        return (binary % modulus).ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0');
    }

    private static byte[] DecodeBase32(string input)
    {
        var cleaned = input.Replace(" ", string.Empty).TrimEnd('=').ToUpperInvariant();
        var bytes = new List<byte>();
        var buffer = 0;
        var bits = 0;

        foreach (var character in cleaned)
        {
            var value = Base32Alphabet.IndexOf(character);
            if (value < 0)
            {
                throw new FormatException($"'{character}' is not a Base32 character.");
            }

            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes.Add((byte)((buffer >> bits) & 0xFF));
            }
        }

        return bytes.ToArray();
    }
}

public class TotpTests
{
    [Fact]
    public void Matches_the_rfc_6238_test_vector()
    {
        // RFC 6238 appendix B: secret "12345678901234567890", T = 59 s. The RFC lists 94287082 for 8 digits.
        const string secret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

        Assert.Equal("94287082", Totp.Compute(secret, DateTimeOffset.FromUnixTimeSeconds(59), digits: 8));
        Assert.Equal("287082", Totp.Compute(secret, DateTimeOffset.FromUnixTimeSeconds(59)));
    }
}
