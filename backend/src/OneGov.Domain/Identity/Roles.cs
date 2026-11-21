namespace OneGov.Domain.Identity;

public static class Roles
{
    public const string Citizen = "citizen";
    public const string Officer = "officer";
    public const string Approver = "approver";
    public const string AgencyAdmin = "agency_admin";
    public const string PlatformAdmin = "platform_admin";

    public static IReadOnlyList<string> All { get; } = [Citizen, Officer, Approver, AgencyAdmin, PlatformAdmin];

    public static bool IsStaff(string role) => role != Citizen && All.Contains(role);
}

/// <summary>
/// The role to permission map. Pure data, no I/O.
/// Separation of duties is deliberate: the officer who reviews and verifies cannot decide, the approver who decides
/// cannot review, and administrators manage the platform but cannot act on citizens' applications.
/// </summary>
public static class RolePermissions
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Map =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [Roles.Citizen] = Set(Permissions.ApplicationSubmit, Permissions.ApplicationReadOwn),
            [Roles.Officer] = Set(Permissions.ApplicationReview, Permissions.ApplicationVerify, Permissions.ApplicationReadAgency),
            [Roles.Approver] = Set(Permissions.ApplicationDecide, Permissions.ApplicationReadAgency),
            [Roles.AgencyAdmin] = Set(Permissions.ApplicationReadAgency, Permissions.ServiceManage, Permissions.UserManage),
            [Roles.PlatformAdmin] = Set(Permissions.ServiceManage, Permissions.UserManage, Permissions.AuditRead),
        };

    public static IReadOnlySet<string> ForRole(string role) =>
        Map.TryGetValue(role, out var permissions) ? permissions : new HashSet<string>(StringComparer.Ordinal);

    public static IReadOnlySet<string> ForRoles(IEnumerable<string> roles)
    {
        var union = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in roles)
        {
            union.UnionWith(ForRole(role));
        }

        return union;
    }

    private static IReadOnlySet<string> Set(params string[] permissions) =>
        new HashSet<string>(permissions, StringComparer.Ordinal);
}
