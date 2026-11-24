namespace OneGov.Domain.Identity;

/// <summary>Who is acting, and what they may do right now. Permissions already account for whether they signed in with MFA.</summary>
public sealed record Actor(Guid UserId, IReadOnlySet<string> Permissions)
{
    public static Actor Create(Guid userId, IEnumerable<string> roles, bool mfaVerified) =>
        new(userId, RolePermissions.Effective(roles, mfaVerified));

    public bool Has(string permission) => Permissions.Contains(permission);
}
