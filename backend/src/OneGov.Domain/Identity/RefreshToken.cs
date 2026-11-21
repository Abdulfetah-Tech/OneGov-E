using OneGov.Domain.Common;

namespace OneGov.Domain.Identity;

public enum RefreshTokenStatus
{
    Active,
    Expired,
    Revoked,
}

/// <summary>
/// A single-use refresh token. Only a hash of the secret is stored. Every successful refresh rotates the token:
/// the old one is revoked and a successor in the same family is issued. Presenting a revoked token again means it was
/// copied or stolen, and the caller then revokes the whole family. Contains no I/O.
/// </summary>
public sealed class RefreshToken : Entity
{
    private RefreshToken()
    {
    }

    public Guid UserId { get; private set; }

    /// <summary>All tokens descended from one sign-in share a family.</summary>
    public Guid FamilyId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    /// <summary>Whether the sign-in that started this family used a second factor. Carried through every rotation.</summary>
    public bool MfaVerified { get; private set; }

    public DateTimeOffset FamilyStartedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public static RefreshToken StartFamily(
        Guid userId, string tokenHash, bool mfaVerified, DateTimeOffset now, TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Lifetime must be positive.");
        }

        return new RefreshToken
        {
            UserId = userId,
            FamilyId = Guid.CreateVersion7(),
            TokenHash = tokenHash,
            MfaVerified = mfaVerified,
            FamilyStartedAt = now,
            CreatedAt = now,
            ExpiresAt = now + lifetime,
        };
    }

    public RefreshTokenStatus Evaluate(DateTimeOffset now)
    {
        if (RevokedAt is not null)
        {
            return RefreshTokenStatus.Revoked;
        }

        return now >= ExpiresAt ? RefreshTokenStatus.Expired : RefreshTokenStatus.Active;
    }

    /// <summary>
    /// Revokes this token and returns its successor. The successor can never outlive the family's absolute maximum age,
    /// so a stolen token cannot be kept alive forever by refreshing it.
    /// </summary>
    public RefreshToken Rotate(string newTokenHash, DateTimeOffset now, TimeSpan lifetime, TimeSpan maxFamilyAge)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newTokenHash);
        if (Evaluate(now) != RefreshTokenStatus.Active)
        {
            throw new InvalidOperationException("Only an active refresh token can be rotated.");
        }

        var requested = now + lifetime;
        var cap = FamilyStartedAt + maxFamilyAge;
        var successor = new RefreshToken
        {
            UserId = UserId,
            FamilyId = FamilyId,
            TokenHash = newTokenHash,
            MfaVerified = MfaVerified,
            FamilyStartedAt = FamilyStartedAt,
            CreatedAt = now,
            ExpiresAt = requested < cap ? requested : cap,
        };

        RevokedAt = now;
        ReplacedByTokenId = successor.Id;
        return successor;
    }
}
