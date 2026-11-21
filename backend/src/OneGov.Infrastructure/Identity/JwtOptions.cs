namespace OneGov.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const int MinimumSigningKeyBytes = 32;

    public string Issuer { get; set; } = "onegov";

    public string Audience { get; set; } = "onegov-web";

    /// <summary>Symmetric HS256 key. Never commit a real value: use user-secrets locally and a secret store elsewhere.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 10;

    public int RefreshTokenDays { get; set; } = 14;

    /// <summary>Absolute limit: a sign-in can be extended by refreshing for at most this long.</summary>
    public int RefreshTokenMaxAgeDays { get; set; } = 30;

    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(AccessTokenMinutes);

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(RefreshTokenDays);

    public TimeSpan RefreshTokenMaxAge => TimeSpan.FromDays(RefreshTokenMaxAgeDays);
}
