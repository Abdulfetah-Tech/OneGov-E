using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OneGov.Application.Audit;
using OneGov.Application.Identity;
using OneGov.Domain.Common;
using OneGov.Domain.Identity;
using OneGov.Infrastructure.Persistence;

namespace OneGov.Infrastructure.Identity;

/// <summary>
/// Local-account authentication on ASP.NET Core Identity: registration, sign-in with lockout and optional TOTP,
/// short-lived access tokens and rotating refresh tokens with reuse detection.
/// </summary>
public sealed class AuthService(
    UserManager<ApplicationUser> users,
    OneGovDbContext db,
    JwtTokenIssuer tokenIssuer,
    JwtOptions jwt,
    IAuditLog audit,
    TimeProvider timeProvider,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    IValidator<RefreshRequest> refreshValidator,
    IValidator<LogoutRequest> logoutValidator,
    IValidator<BeginMfaRequest> beginMfaValidator,
    IValidator<ConfirmMfaRequest> confirmMfaValidator) : IAuthService
{
    private const int RecoveryCodeCount = 10;
    private const string AuthenticatorIssuer = "OneGov";

    // Used to spend the same time on unknown accounts as on real ones, so response timing does not reveal which emails exist.
    private static readonly PasswordHasher<ApplicationUser> TimingHasher = new();

    private static readonly string TimingHash =
        TimingHasher.HashPassword(new ApplicationUser(), "timing-equalisation-only");

    public async Task<Result<UserProfileDto>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var validation = await registerValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<UserProfileDto>.Failure(Invalid(validation));
        }

        var email = request.Email.Trim();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            PreferredLanguage = request.Language,
        };

        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            return Result<UserProfileDto>.Failure(MapIdentityErrors(created.Errors));
        }

        await users.AddToRoleAsync(user, Roles.Citizen);
        await AuditAsync(user, "auth.register", cancellationToken);
        return Result<UserProfileDto>.Success(await ToProfileAsync(user));
    }

    public async Task<Result<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var validation = await loginValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<TokenResponse>.Failure(Invalid(validation));
        }

        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive)
        {
            TimingHasher.VerifyHashedPassword(user ?? new ApplicationUser(), TimingHash, request.Password);
            return InvalidCredentials<TokenResponse>();
        }

        // A locked account gets the same answer as a wrong password, and the password is not checked while locked.
        if (await users.IsLockedOutAsync(user))
        {
            return InvalidCredentials<TokenResponse>();
        }

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            await AuditAsync(user, "auth.login_failed", cancellationToken);
            return InvalidCredentials<TokenResponse>();
        }

        var otpVerified = false;
        if (user.TwoFactorEnabled)
        {
            if (string.IsNullOrWhiteSpace(request.OtpCode))
            {
                return Result<TokenResponse>.Failure(AuthErrors.MfaRequired, "A one-time code is required.");
            }

            // Wrong codes count toward lockout so a six-digit code cannot be guessed by brute force.
            if (!await VerifyOtpAsync(user, request.OtpCode))
            {
                await users.AccessFailedAsync(user);
                await AuditAsync(user, "auth.login_failed", cancellationToken);
                return InvalidCredentials<TokenResponse>();
            }

            otpVerified = true;
        }

        await users.ResetAccessFailedCountAsync(user);
        var tokens = await IssueTokensAsync(user, otpVerified, cancellationToken);
        await AuditAsync(user, otpVerified ? "auth.login_mfa" : "auth.login", cancellationToken);
        return Result<TokenResponse>.Success(tokens);
    }

    public async Task<Result<TokenResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        var validation = await refreshValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<TokenResponse>.Failure(Invalid(validation));
        }

        var hash = RefreshTokenSecret.Hash(request.RefreshToken);
        var token = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is null)
        {
            return InvalidToken<TokenResponse>();
        }

        var now = timeProvider.GetUtcNow();
        switch (token.Evaluate(now))
        {
            case RefreshTokenStatus.Revoked:
                // A token that was already rotated or revoked is being used again: assume it leaked.
                await RevokeFamilyAsync(token.FamilyId, now, cancellationToken);
                await AuditReuseAsync(token, cancellationToken);
                return InvalidToken<TokenResponse>();
            case RefreshTokenStatus.Expired:
                return InvalidToken<TokenResponse>();
        }

        var user = await users.FindByIdAsync(token.UserId.ToString());
        if (user is null || !user.IsActive || await users.IsLockedOutAsync(user))
        {
            await RevokeFamilyAsync(token.FamilyId, now, cancellationToken);
            return InvalidToken<TokenResponse>();
        }

        var (plain, newHash) = RefreshTokenSecret.Generate();
        var successor = token.Rotate(newHash, now, jwt.RefreshTokenLifetime, jwt.RefreshTokenMaxAge);
        db.RefreshTokens.Add(successor);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request rotated the same token first. Treat the loser like a reuse attempt.
            db.ChangeTracker.Clear();
            await RevokeFamilyAsync(token.FamilyId, now, cancellationToken);
            await AuditReuseAsync(token, cancellationToken);
            return InvalidToken<TokenResponse>();
        }

        var roles = await users.GetRolesAsync(user);
        var access = tokenIssuer.Issue(user, roles, token.MfaVerified);
        return Result<TokenResponse>.Success(
            new TokenResponse(access.Token, access.ExpiresAt, plain, successor.ExpiresAt));
    }

    public async Task<Result<bool>> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        var validation = await logoutValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<bool>.Failure(Invalid(validation));
        }

        var hash = RefreshTokenSecret.Hash(request.RefreshToken);
        var token = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is not null)
        {
            await RevokeFamilyAsync(token.FamilyId, timeProvider.GetUtcNow(), cancellationToken);
            await audit.AppendAsync(token.UserId.ToString(), "auth.logout", $"user:{token.UserId}", null, cancellationToken);
        }

        // Always succeeds, so the endpoint cannot be used to test whether a token is valid.
        return Result<bool>.Success(true);
    }

    public async Task<Result<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await FindActiveAsync(userId);
        return user is null
            ? Result<UserProfileDto>.Failure(AuthErrors.NotFound, "Account not found.")
            : Result<UserProfileDto>.Success(await ToProfileAsync(user));
    }

    public async Task<Result<MfaEnrollmentDto>> BeginMfaEnrollmentAsync(
        Guid userId, BeginMfaRequest request, CancellationToken cancellationToken)
    {
        var validation = await beginMfaValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<MfaEnrollmentDto>.Failure(Invalid(validation));
        }

        var user = await FindActiveAsync(userId);
        if (user is null)
        {
            return Result<MfaEnrollmentDto>.Failure(AuthErrors.NotFound, "Account not found.");
        }

        // Re-check the password. Wrong attempts count toward lockout, so this endpoint cannot be used to guess it.
        if (await users.IsLockedOutAsync(user) || !await users.CheckPasswordAsync(user, request.Password))
        {
            if (!await users.IsLockedOutAsync(user))
            {
                await users.AccessFailedAsync(user);
            }

            await AuditAsync(user, "auth.mfa_enroll_reauth_failed", cancellationToken);
            return Result<MfaEnrollmentDto>.Failure(
                AuthErrors.ReauthenticationFailed, "The password is not correct.");
        }

        if (user.TwoFactorEnabled)
        {
            return Result<MfaEnrollmentDto>.Failure(AuthErrors.MfaAlreadyEnabled, "Multi-factor sign-in is already enabled.");
        }

        await users.ResetAuthenticatorKeyAsync(user);
        var key = await users.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            return Result<MfaEnrollmentDto>.Failure(AuthErrors.InvalidRequest, "Could not create an authenticator key.");
        }

        var account = user.Email ?? user.UserName ?? user.Id.ToString();
        var uri = $"otpauth://totp/{Uri.EscapeDataString(AuthenticatorIssuer)}:{Uri.EscapeDataString(account)}"
            + $"?secret={key}&issuer={Uri.EscapeDataString(AuthenticatorIssuer)}&algorithm=SHA1&digits=6&period=30";
        return Result<MfaEnrollmentDto>.Success(new MfaEnrollmentDto(key, uri));
    }

    public async Task<Result<MfaConfirmedDto>> ConfirmMfaEnrollmentAsync(
        Guid userId, ConfirmMfaRequest request, CancellationToken cancellationToken)
    {
        var validation = await confirmMfaValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<MfaConfirmedDto>.Failure(Invalid(validation));
        }

        var user = await FindActiveAsync(userId);
        if (user is null)
        {
            return Result<MfaConfirmedDto>.Failure(AuthErrors.NotFound, "Account not found.");
        }

        if (user.TwoFactorEnabled)
        {
            return Result<MfaConfirmedDto>.Failure(AuthErrors.MfaAlreadyEnabled, "Multi-factor sign-in is already enabled.");
        }

        if (string.IsNullOrEmpty(await users.GetAuthenticatorKeyAsync(user)))
        {
            return Result<MfaConfirmedDto>.Failure(AuthErrors.MfaNotStarted, "Start enrolment before confirming a code.");
        }

        var code = request.Code.Replace(" ", string.Empty);
        if (!IsSixDigits(code)
            || !await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, code))
        {
            return Result<MfaConfirmedDto>.Failure(AuthErrors.InvalidOtp, "The code is not valid.");
        }

        await users.SetTwoFactorEnabledAsync(user, true);
        var recoveryCodes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        await AuditAsync(user, "auth.mfa_enabled", cancellationToken);
        return Result<MfaConfirmedDto>.Success(new MfaConfirmedDto((recoveryCodes ?? []).ToList()));
    }

    private async Task<bool> VerifyOtpAsync(ApplicationUser user, string code)
    {
        var compact = code.Trim().Replace(" ", string.Empty);
        if (IsSixDigits(compact))
        {
            return await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, compact);
        }

        // Anything else is treated as a single-use recovery code.
        var redeemed = await users.RedeemTwoFactorRecoveryCodeAsync(user, code.Trim());
        return redeemed.Succeeded;
    }

    private async Task<TokenResponse> IssueTokensAsync(ApplicationUser user, bool mfaVerified, CancellationToken cancellationToken)
    {
        var roles = await users.GetRolesAsync(user);
        var access = tokenIssuer.Issue(user, roles, mfaVerified);
        var (plain, hash) = RefreshTokenSecret.Generate();
        var token = RefreshToken.StartFamily(user.Id, hash, mfaVerified, timeProvider.GetUtcNow(), jwt.RefreshTokenLifetime);
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);
        return new TokenResponse(access.Token, access.ExpiresAt, plain, token.ExpiresAt);
    }

    private Task<int> RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, (DateTimeOffset?)now), cancellationToken);

    private async Task AuditReuseAsync(RefreshToken token, CancellationToken cancellationToken) =>
        await audit.AppendAsync(
            token.UserId.ToString(), "auth.refresh_reuse_detected", $"user:{token.UserId}", null, cancellationToken);

    private async Task AuditAsync(ApplicationUser user, string action, CancellationToken cancellationToken) =>
        await audit.AppendAsync(user.Id.ToString(), action, $"user:{user.Id}", null, cancellationToken);

    private async Task<ApplicationUser?> FindActiveAsync(Guid userId)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        return user is { IsActive: true } ? user : null;
    }

    private async Task<UserProfileDto> ToProfileAsync(ApplicationUser user)
    {
        var roles = (await users.GetRolesAsync(user)).OrderBy(r => r, StringComparer.Ordinal).ToList();
        var permissions = RolePermissions.ForRoles(roles).OrderBy(p => p, StringComparer.Ordinal).ToList();
        return new UserProfileDto(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            user.PreferredLanguage,
            roles,
            permissions,
            user.TwoFactorEnabled,
            user.AgencyId);
    }

    private static bool IsSixDigits(string value) => value.Length == 6 && value.All(char.IsAsciiDigit);

    private static Error Invalid(ValidationResult validation) =>
        new(AuthErrors.InvalidRequest, string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));

    private static Result<T> InvalidCredentials<T>() =>
        Result<T>.Failure(AuthErrors.InvalidCredentials, "Email or password is incorrect, or the account is temporarily locked.");

    private static Result<T> InvalidToken<T>() =>
        Result<T>.Failure(AuthErrors.InvalidToken, "The refresh token is not valid. Sign in again.");

    private static Error MapIdentityErrors(IEnumerable<IdentityError> errors)
    {
        var list = errors.ToList();
        if (list.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
        {
            return new Error(AuthErrors.EmailTaken, "An account with this email already exists.");
        }

        var passwordErrors = list.Where(e => e.Code.StartsWith("Password", StringComparison.Ordinal)).ToList();
        if (passwordErrors.Count > 0)
        {
            return new Error(AuthErrors.WeakPassword, string.Join(" ", passwordErrors.Select(e => e.Description)));
        }

        return new Error(AuthErrors.InvalidRequest, string.Join(" ", list.Select(e => e.Description)));
    }
}
