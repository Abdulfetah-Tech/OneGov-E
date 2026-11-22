using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace OneGov.Infrastructure.Identity;

public sealed record IssuedAccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>Creates short-lived signed access tokens. Permissions are not embedded: they are derived from roles on each request.</summary>
public sealed class JwtTokenIssuer(JwtOptions options, TimeProvider timeProvider)
{
    private readonly SigningCredentials _credentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)), SecurityAlgorithms.HmacSha256);

    private readonly JsonWebTokenHandler _handler = new();

    public IssuedAccessToken Issue(ApplicationUser user, IEnumerable<string> roles, bool mfaVerified)
    {
        var now = timeProvider.GetUtcNow();
        var expires = now.Add(options.AccessTokenLifetime);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            // Accounts created through the national ID have no password, so they can only have signed in that way.
            new("amr", user.PasswordHash is null ? "eid" : "pwd"),
        };

        if (mfaVerified)
        {
            claims.Add(new Claim("amr", "otp"));
        }

        if (user.AgencyId is { } agencyId)
        {
            claims.Add(new Claim("agency", agencyId.ToString()));
        }

        claims.AddRange(roles.Select(role => new Claim("role", role)));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = _credentials,
        };

        return new IssuedAccessToken(_handler.CreateToken(descriptor), expires);
    }
}

/// <summary>Generates opaque refresh-token secrets. Only the SHA-256 hash is ever stored.</summary>
public static class RefreshTokenSecret
{
    public static (string Plain, string Hash) Generate()
    {
        var plain = Base64Url(RandomNumberGenerator.GetBytes(32));
        return (plain, Hash(plain));
    }

    public static string Hash(string plain) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(plain)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
