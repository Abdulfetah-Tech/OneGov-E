namespace OneGov.Domain.Identity;

/// <summary>
/// Which applications the current caller may see: their own as a citizen, and/or one agency's as staff.
/// An empty id never matches real data, so an absent scope means no access. Pure, no I/O.
/// </summary>
public sealed record DataScope(bool IsUnrestricted, Guid CitizenId, Guid AgencyId)
{
    /// <summary>No access. This is the default for anonymous callers and for code that has not stated a scope.</summary>
    public static DataScope None { get; } = new(false, Guid.Empty, Guid.Empty);

    /// <summary>Unrestricted. Reserved for trusted system code such as migrations and seeders. Never derived from a request.</summary>
    public static DataScope Everything { get; } = new(true, Guid.Empty, Guid.Empty);

    /// <summary>
    /// Derives a caller's scope from their roles. Staff access needs a multi-factor sign-in, a role that may read agency
    /// applications and an agency to belong to. Staff without an agency, and administrators, get no application access.
    /// </summary>
    public static DataScope For(Guid? userId, Guid? agencyId, IEnumerable<string> roles, bool mfaVerified)
    {
        if (userId is not { } id)
        {
            return None;
        }

        var permissions = RolePermissions.ForRoles(roles)
            .Where(permission => mfaVerified || !Permissions.RequiresMfa(permission))
            .ToHashSet(StringComparer.Ordinal);

        var citizen = permissions.Contains(Permissions.ApplicationReadOwn) ? id : Guid.Empty;
        var agency = permissions.Contains(Permissions.ApplicationReadAgency) && agencyId is { } agencyGuid
            ? agencyGuid
            : Guid.Empty;

        return new DataScope(false, citizen, agency);
    }
}
