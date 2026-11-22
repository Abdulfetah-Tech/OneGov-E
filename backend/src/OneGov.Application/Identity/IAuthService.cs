using OneGov.Domain.Common;

namespace OneGov.Application.Identity;

/// <summary>Port for local-account authentication. Implemented in Infrastructure on top of ASP.NET Core Identity.</summary>
public interface IAuthService
{
    Task<Result<UserProfileDto>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<Result<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<Result<TokenResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken);

    Task<Result<bool>> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken);

    Task<Result<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken cancellationToken);

    NationalIdStatusDto GetNationalIdStatus();

    Result<NationalIdStartDto> BeginNationalId();

    Task<Result<TokenResponse>> CompleteNationalIdAsync(
        NationalIdCompleteRequest request, CancellationToken cancellationToken);

    Task<Result<MfaEnrollmentDto>> BeginMfaEnrollmentAsync(
        Guid userId, BeginMfaRequest request, CancellationToken cancellationToken);

    Task<Result<MfaConfirmedDto>> ConfirmMfaEnrollmentAsync(
        Guid userId, ConfirmMfaRequest request, CancellationToken cancellationToken);
}

/// <summary>Stable error codes returned by <see cref="IAuthService"/>.</summary>
public static class AuthErrors
{
    public const string InvalidRequest = "identity.invalid_request";
    public const string WeakPassword = "identity.weak_password";
    public const string EmailTaken = "identity.email_taken";
    public const string InvalidCredentials = "identity.invalid_credentials";
    public const string MfaRequired = "identity.mfa_required";
    public const string InvalidToken = "identity.invalid_token";
    public const string InvalidOtp = "identity.invalid_otp";
    public const string ReauthenticationFailed = "identity.reauthentication_failed";
    public const string MfaAlreadyEnabled = "identity.mfa_already_enabled";
    public const string MfaNotStarted = "identity.mfa_not_started";
    public const string NotFound = "identity.not_found";
    public const string InvalidState = "identity.invalid_state";
    public const string NationalIdFailed = "identity.national_id_failed";
    public const string NationalIdUnavailable = "identity.national_id_unavailable";
}
