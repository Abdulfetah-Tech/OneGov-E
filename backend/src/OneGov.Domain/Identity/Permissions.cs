using OneGov.Domain.Workflow;

namespace OneGov.Domain.Identity;

/// <summary>
/// Permission identifiers. Workflow permissions reuse the identifiers already used by workflow transitions,
/// so a role that is granted a permission here can drive the matching transitions.
/// </summary>
public static class Permissions
{
    public const string ApplicationSubmit = WorkflowPermissions.Submit;
    public const string ApplicationReview = WorkflowPermissions.Review;
    public const string ApplicationVerify = WorkflowPermissions.Verify;
    public const string ApplicationDecide = WorkflowPermissions.Decide;
    public const string ApplicationReadOwn = "application:read_own";
    public const string ApplicationReadAgency = "application:read_agency";
    public const string ServiceManage = "service:manage";
    public const string UserManage = "user:manage";
    public const string AuditRead = "audit:read";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        ApplicationSubmit,
        ApplicationReview,
        ApplicationVerify,
        ApplicationDecide,
        ApplicationReadOwn,
        ApplicationReadAgency,
        ServiceManage,
        UserManage,
        AuditRead,
    };

    /// <summary>Permissions that exist only for staff. Using them requires a multi-factor sign-in.</summary>
    public static IReadOnlySet<string> StaffOnly { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        ApplicationReview,
        ApplicationVerify,
        ApplicationDecide,
        ApplicationReadAgency,
        ServiceManage,
        UserManage,
        AuditRead,
    };

    public static bool RequiresMfa(string permission) => StaffOnly.Contains(permission);
}
