namespace OneGov.Application.Identity;

public sealed record RegisterRequest(string Email, string Password, string DisplayName, string Language = "en");

/// <summary>Sign in. <see cref="OtpCode"/> is a 6-digit authenticator code or a recovery code, required when MFA is on.</summary>
public sealed record LoginRequest(string Email, string Password, string? OtpCode = null);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

/// <summary>Starting MFA enrolment re-checks the account password, so a stolen access token alone cannot enrol a second factor.</summary>
public sealed record BeginMfaRequest(string Password);

public sealed record ConfirmMfaRequest(string Code);

public sealed record TokenResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    string TokenType = "Bearer");

public sealed record UserProfileDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Language,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    bool MfaEnabled,
    Guid? AgencyId);

public sealed record MfaEnrollmentDto(string SharedKey, string OtpAuthUri);

public sealed record MfaConfirmedDto(IReadOnlyList<string> RecoveryCodes);
