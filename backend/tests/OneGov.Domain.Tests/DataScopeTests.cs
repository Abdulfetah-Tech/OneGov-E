using OneGov.Domain.Identity;

namespace OneGov.Domain.Tests;

public class DataScopeTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Agency = Guid.NewGuid();

    [Fact]
    public void Anonymous_callers_get_no_access()
    {
        var scope = DataScope.For(null, null, [], mfaVerified: false);

        Assert.Equal(DataScope.None, scope);
        Assert.False(scope.IsUnrestricted);
    }

    [Fact]
    public void Citizens_see_only_their_own_applications()
    {
        var scope = DataScope.For(User, null, [Roles.Citizen], mfaVerified: false);

        Assert.Equal(User, scope.CitizenId);
        Assert.Equal(Guid.Empty, scope.AgencyId);
        Assert.False(scope.IsUnrestricted);
    }

    [Theory]
    [InlineData(Roles.Officer)]
    [InlineData(Roles.Approver)]
    [InlineData(Roles.AgencyAdmin)]
    public void Staff_with_mfa_see_their_agency_but_not_other_citizens(string role)
    {
        var scope = DataScope.For(User, Agency, [role], mfaVerified: true);

        Assert.Equal(Agency, scope.AgencyId);
        Assert.Equal(Guid.Empty, scope.CitizenId);
    }

    [Fact]
    public void Staff_without_mfa_see_nothing()
    {
        var scope = DataScope.For(User, Agency, [Roles.Officer], mfaVerified: false);

        Assert.Equal(DataScope.None, scope);
    }

    [Fact]
    public void Staff_without_an_agency_see_nothing()
    {
        var scope = DataScope.For(User, null, [Roles.Officer], mfaVerified: true);

        Assert.Equal(DataScope.None, scope);
    }

    [Fact]
    public void Platform_administrators_see_no_applications()
    {
        var scope = DataScope.For(User, null, [Roles.PlatformAdmin], mfaVerified: true);

        Assert.Equal(DataScope.None, scope);
    }

    [Fact]
    public void A_staff_member_who_is_also_a_citizen_gets_both_scopes()
    {
        var scope = DataScope.For(User, Agency, [Roles.Citizen, Roles.Officer], mfaVerified: true);

        Assert.Equal(User, scope.CitizenId);
        Assert.Equal(Agency, scope.AgencyId);
    }

    [Fact]
    public void Only_the_explicit_system_scope_is_unrestricted()
    {
        Assert.True(DataScope.Everything.IsUnrestricted);
        Assert.All(
            Roles.All,
            role => Assert.False(DataScope.For(User, Agency, [role], mfaVerified: true).IsUnrestricted));
    }
}
