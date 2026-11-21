using FluentValidation;
using OneGov.Application.Registry;

namespace OneGov.Application.Identity;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().MaximumLength(254).EmailAddress();
        // Complexity rules live in the identity provider's password options; this only bounds the input size.
        RuleFor(r => r.Password).NotEmpty().MaximumLength(128);
        RuleFor(r => r.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(r => r.Language)
            .Must(language => ListServicesQueryValidator.SupportedLanguages.Contains(language))
            .WithMessage("Language must be one of: en, am, om.");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().MaximumLength(254);
        RuleFor(r => r.Password).NotEmpty().MaximumLength(128);
        RuleFor(r => r.OtpCode).MaximumLength(32);
    }
}

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator() => RuleFor(r => r.RefreshToken).NotEmpty().MaximumLength(200);
}

public sealed class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator() => RuleFor(r => r.RefreshToken).NotEmpty().MaximumLength(200);
}

public sealed class ConfirmMfaRequestValidator : AbstractValidator<ConfirmMfaRequest>
{
    public ConfirmMfaRequestValidator() => RuleFor(r => r.Code).NotEmpty().MaximumLength(16);
}
