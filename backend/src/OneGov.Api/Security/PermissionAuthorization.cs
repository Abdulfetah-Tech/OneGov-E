using Microsoft.AspNetCore.Authorization;
using OneGov.Domain.Identity;

namespace OneGov.Api.Security;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

/// <summary>
/// Grants access when one of the caller's roles carries the permission. Staff-only permissions also need a token that
/// was issued after a second factor (amr contains "otp"). Permissions come from roles on every request, so changing the
/// role map takes effect without waiting for tokens to be re-issued.
/// </summary>
public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var roles = context.User.FindAll("role").Select(claim => claim.Value);
        var allowed = RolePermissions.ForRoles(roles).Contains(requirement.Permission);
        var mfaSatisfied = !Permissions.RequiresMfa(requirement.Permission) || context.User.HasClaim("amr", "otp");

        if (allowed && mfaSatisfied)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>Policy names for <c>[Authorize(Policy = ...)]</c>. One policy exists per permission.</summary>
public static class PermissionPolicies
{
    public const string Prefix = "perm:";

    public const string ApplicationSubmit = Prefix + Permissions.ApplicationSubmit;
    public const string ApplicationReview = Prefix + Permissions.ApplicationReview;
    public const string ApplicationVerify = Prefix + Permissions.ApplicationVerify;
    public const string ApplicationDecide = Prefix + Permissions.ApplicationDecide;
    public const string ApplicationReadOwn = Prefix + Permissions.ApplicationReadOwn;
    public const string ApplicationReadAgency = Prefix + Permissions.ApplicationReadAgency;
    public const string ServiceManage = Prefix + Permissions.ServiceManage;
    public const string UserManage = Prefix + Permissions.UserManage;
    public const string AuditRead = Prefix + Permissions.AuditRead;
}

public static class PermissionAuthorizationExtensions
{
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
        services.AddAuthorization(options =>
        {
            foreach (var permission in Permissions.All)
            {
                options.AddPolicy(
                    PermissionPolicies.Prefix + permission,
                    policy => policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(permission)));
            }
        });
        return services;
    }
}
