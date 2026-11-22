using FluentValidation;
using OneGov.Domain.Common;

namespace OneGov.Application.Identity;

/// <summary>Who the national ID provider says the person is. <see cref="Subject"/> is a stable pseudonym, never the ID number.</summary>
public sealed record NationalIdentity(string Subject, string FullName);

/// <summary>
/// Port for signing in with the national digital ID (Fayda). Implementations: a disabled one, a mock for development and
/// tests, and later the real OpenID Connect adapter, which needs onboarding with the identity provider first.
/// </summary>
public interface INationalIdProvider
{
    bool IsAvailable { get; }

    bool IsMock { get; }

    /// <summary>Where to send the person's browser. The provider sends it back with a code and the same state.</summary>
    Uri BuildAuthorizationUri(string state);

    Task<Result<NationalIdentity>> ExchangeCodeAsync(string code, CancellationToken cancellationToken);
}

public sealed record NationalIdStatusDto(bool Available, bool IsMock);

public sealed record NationalIdStartDto(string AuthorizationUrl, string State);

public sealed record NationalIdCompleteRequest(string Code, string State);

public sealed class NationalIdCompleteRequestValidator : AbstractValidator<NationalIdCompleteRequest>
{
    public NationalIdCompleteRequestValidator()
    {
        RuleFor(r => r.Code).NotEmpty().MaximumLength(4096);
        RuleFor(r => r.State).NotEmpty().MaximumLength(2048);
    }
}
