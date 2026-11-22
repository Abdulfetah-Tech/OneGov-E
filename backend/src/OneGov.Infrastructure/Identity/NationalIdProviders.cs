using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OneGov.Application.Identity;
using OneGov.Domain.Common;

namespace OneGov.Infrastructure.Identity;

public sealed class NationalIdOptions
{
    /// <summary>none (default) or mock. The real Fayda adapter is not built yet.</summary>
    public string Provider { get; set; } = "none";

    /// <summary>The web app page that receives the provider's redirect. Fixed on the server, never taken from the client.</summary>
    public string RedirectUri { get; set; } = "http://localhost:4200/auth/national-id/callback";
}

public sealed class DisabledNationalIdProvider : INationalIdProvider
{
    public bool IsAvailable => false;

    public bool IsMock => false;

    public Uri BuildAuthorizationUri(string state) =>
        throw new InvalidOperationException("National ID sign-in is not configured.");

    public Task<Result<NationalIdentity>> ExchangeCodeAsync(string code, CancellationToken cancellationToken) =>
        Task.FromResult(Result<NationalIdentity>.Failure(
            AuthErrors.NationalIdUnavailable, "National ID sign-in is not available."));
}

/// <summary>
/// DEVELOPMENT AND TESTS ONLY. Stands in for the national ID provider so the whole sign-in flow can be built and tested.
/// Its "authorization code" simply contains the identity, so anyone can sign in as anyone. The API refuses to start with
/// this provider outside Development and Testing.
/// </summary>
public sealed class MockNationalIdProvider(NationalIdOptions options) : INationalIdProvider
{
    private const string Prefix = "mock.";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record MockClaims(string IdNumber, string FullName);

    public bool IsAvailable => true;

    public bool IsMock => true;

    public Uri BuildAuthorizationUri(string state)
    {
        var origin = new Uri(options.RedirectUri).GetLeftPart(UriPartial.Authority);
        return new Uri($"{origin}/auth/national-id/mock?state={Uri.EscapeDataString(state)}");
    }

    /// <summary>Builds the code the mock "provider" would return. The web app's mock page does the same in JavaScript.</summary>
    public static string BuildCode(string idNumber, string fullName) =>
        Prefix + Base64Url.EncodeToString(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new MockClaims(idNumber, fullName), Json)));

    public Task<Result<NationalIdentity>> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
    {
        try
        {
            if (!code.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return Failed();
            }

            var json = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(code.AsSpan(Prefix.Length)));
            var claims = JsonSerializer.Deserialize<MockClaims>(json, Json);
            if (claims is null || string.IsNullOrWhiteSpace(claims.IdNumber) || string.IsNullOrWhiteSpace(claims.FullName))
            {
                return Failed();
            }

            // A pseudonym derived from the number, so the number itself is never stored.
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(claims.IdNumber.Trim())));
            return Task.FromResult(Result<NationalIdentity>.Success(
                new NationalIdentity("mock-" + hash[..32], claims.FullName.Trim())));
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return Failed();
        }
    }

    private static Task<Result<NationalIdentity>> Failed() =>
        Task.FromResult(Result<NationalIdentity>.Failure(
            AuthErrors.NationalIdFailed, "The national ID sign-in could not be completed."));
}
