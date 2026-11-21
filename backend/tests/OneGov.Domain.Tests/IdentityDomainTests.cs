using OneGov.Domain.Identity;
using OneGov.Domain.Workflow;

namespace OneGov.Domain.Tests;

public class RolePermissionTests
{
    [Fact]
    public void Citizens_can_submit_but_cannot_work_cases()
    {
        var permissions = RolePermissions.ForRole(Roles.Citizen);

        Assert.Contains(Permissions.ApplicationSubmit, permissions);
        Assert.DoesNotContain(Permissions.ApplicationReview, permissions);
        Assert.DoesNotContain(Permissions.ApplicationDecide, permissions);
    }

    [Fact]
    public void Every_workflow_permission_is_granted_to_some_role()
    {
        var granted = RolePermissions.ForRoles(Roles.All);
        string[] workflow =
        [
            WorkflowPermissions.Submit, WorkflowPermissions.Review, WorkflowPermissions.Verify, WorkflowPermissions.Decide,
        ];

        Assert.All(workflow, permission => Assert.Contains(permission, granted));
    }

    [Fact]
    public void No_role_can_both_review_and_decide()
    {
        foreach (var role in Roles.All)
        {
            var permissions = RolePermissions.ForRole(role);
            Assert.False(
                permissions.Contains(Permissions.ApplicationReview) && permissions.Contains(Permissions.ApplicationDecide),
                $"{role} can both review and decide");
        }
    }

    [Fact]
    public void Platform_administrators_cannot_act_on_applications()
    {
        var permissions = RolePermissions.ForRole(Roles.PlatformAdmin);

        Assert.DoesNotContain(Permissions.ApplicationSubmit, permissions);
        Assert.DoesNotContain(Permissions.ApplicationReview, permissions);
        Assert.DoesNotContain(Permissions.ApplicationVerify, permissions);
        Assert.DoesNotContain(Permissions.ApplicationDecide, permissions);
    }

    [Fact]
    public void Roles_only_reference_known_permissions_and_none_is_empty()
    {
        foreach (var role in Roles.All)
        {
            var permissions = RolePermissions.ForRole(role);
            Assert.NotEmpty(permissions);
            Assert.All(permissions, permission => Assert.Contains(permission, Permissions.All));
        }
    }

    [Fact]
    public void Unknown_roles_have_no_permissions() =>
        Assert.Empty(RolePermissions.ForRole("made_up_role"));

    [Fact]
    public void Staff_permissions_require_mfa_and_citizen_permissions_do_not()
    {
        Assert.True(Permissions.RequiresMfa(Permissions.ApplicationDecide));
        Assert.True(Permissions.RequiresMfa(Permissions.AuditRead));
        Assert.False(Permissions.RequiresMfa(Permissions.ApplicationSubmit));
        Assert.False(Permissions.RequiresMfa(Permissions.ApplicationReadOwn));
    }

    [Fact]
    public void Any_role_holding_a_staff_permission_is_a_staff_role()
    {
        foreach (var role in Roles.All)
        {
            var hasStaffPermission = RolePermissions.ForRole(role).Overlaps(Permissions.StaffOnly);
            Assert.Equal(hasStaffPermission, Roles.IsStaff(role));
        }
    }

    [Fact]
    public void Combining_roles_unions_their_permissions()
    {
        var combined = RolePermissions.ForRoles([Roles.Officer, Roles.Approver]);

        Assert.Contains(Permissions.ApplicationReview, combined);
        Assert.Contains(Permissions.ApplicationDecide, combined);
    }
}

public class RefreshTokenTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(14);
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);

    [Fact]
    public void A_new_token_is_active_until_it_expires()
    {
        var token = RefreshToken.StartFamily(Guid.NewGuid(), "hash-1", mfaVerified: false, Start, Lifetime);

        Assert.Equal(RefreshTokenStatus.Active, token.Evaluate(Start.AddDays(13)));
        Assert.Equal(RefreshTokenStatus.Expired, token.Evaluate(Start.AddDays(14)));
    }

    [Fact]
    public void Rotating_revokes_the_old_token_and_keeps_the_family()
    {
        var token = RefreshToken.StartFamily(Guid.NewGuid(), "hash-1", mfaVerified: false, Start, Lifetime);
        var later = Start.AddHours(1);

        var successor = token.Rotate("hash-2", later, Lifetime, MaxAge);

        Assert.Equal(RefreshTokenStatus.Revoked, token.Evaluate(later));
        Assert.Equal(successor.Id, token.ReplacedByTokenId);
        Assert.Equal(RefreshTokenStatus.Active, successor.Evaluate(later));
        Assert.Equal(token.FamilyId, successor.FamilyId);
        Assert.Equal(token.UserId, successor.UserId);
        Assert.NotEqual(token.Id, successor.Id);
    }

    [Fact]
    public void A_revoked_token_cannot_be_rotated_again()
    {
        var token = RefreshToken.StartFamily(Guid.NewGuid(), "hash-1", mfaVerified: false, Start, Lifetime);
        token.Rotate("hash-2", Start.AddHours(1), Lifetime, MaxAge);

        Assert.Throws<InvalidOperationException>(() => token.Rotate("hash-3", Start.AddHours(2), Lifetime, MaxAge));
    }

    [Fact]
    public void An_expired_token_cannot_be_rotated()
    {
        var token = RefreshToken.StartFamily(Guid.NewGuid(), "hash-1", mfaVerified: false, Start, Lifetime);

        Assert.Throws<InvalidOperationException>(() => token.Rotate("hash-2", Start.AddDays(15), Lifetime, MaxAge));
    }

    [Fact]
    public void A_successor_cannot_outlive_the_familys_absolute_maximum_age()
    {
        var first = RefreshToken.StartFamily(Guid.NewGuid(), "hash-1", mfaVerified: false, Start, Lifetime);
        var second = first.Rotate("hash-2", Start.AddDays(10), Lifetime, MaxAge);
        var third = second.Rotate("hash-3", Start.AddDays(20), Lifetime, MaxAge);

        Assert.Equal(Start.AddDays(24), second.ExpiresAt);
        Assert.Equal(Start.AddDays(30), third.ExpiresAt);
        Assert.Equal(RefreshTokenStatus.Expired, third.Evaluate(Start.AddDays(30)));
    }

    [Fact]
    public void The_mfa_flag_is_carried_through_rotation()
    {
        var token = RefreshToken.StartFamily(Guid.NewGuid(), "hash-1", mfaVerified: true, Start, Lifetime);

        var successor = token.Rotate("hash-2", Start.AddHours(1), Lifetime, MaxAge);

        Assert.True(successor.MfaVerified);
    }

    [Fact]
    public void A_blank_hash_is_rejected() =>
        Assert.ThrowsAny<ArgumentException>(() =>
            RefreshToken.StartFamily(Guid.NewGuid(), " ", mfaVerified: false, Start, Lifetime));
}
