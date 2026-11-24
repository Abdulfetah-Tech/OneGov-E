using OneGov.Domain.Applications;
using OneGov.Domain.Identity;
using OneGov.Domain.Workflow;

namespace OneGov.Domain.Tests;

public class ActorTests
{
    [Fact]
    public void Staff_permissions_only_count_after_a_multi_factor_sign_in()
    {
        var withoutMfa = Actor.Create(Guid.NewGuid(), [Roles.Officer], mfaVerified: false);
        var withMfa = Actor.Create(Guid.NewGuid(), [Roles.Officer], mfaVerified: true);

        Assert.False(withoutMfa.Has(Permissions.ApplicationReview));
        Assert.True(withMfa.Has(Permissions.ApplicationReview));
    }

    [Fact]
    public void Citizen_permissions_do_not_need_mfa() =>
        Assert.True(Actor.Create(Guid.NewGuid(), [Roles.Citizen], mfaVerified: false).Has(Permissions.ApplicationSubmit));
}

public class ApplicationActorRulesTests
{
    private static readonly WorkflowDefinition Tax = BuiltInWorkflows.TaxFiling;
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 8, 0, 0, TimeSpan.Zero);

    private static ServiceApplication Draft(Guid citizenId) =>
        ServiceApplication.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), citizenId, Tax);

    private static Actor Citizen(Guid id) => Actor.Create(id, [Roles.Citizen], mfaVerified: false);

    private static Actor Staff(Guid id, string role) => Actor.Create(id, [role], mfaVerified: true);

    [Fact]
    public void The_applicant_can_submit()
    {
        var citizenId = Guid.NewGuid();
        var application = Draft(citizenId);

        Assert.True(application.ApplyAs(Tax, "submit", Citizen(citizenId), null, Now).IsSuccess);
        Assert.Equal("submitted", application.State);
    }

    [Fact]
    public void Someone_else_cannot_act_as_the_applicant_even_with_the_permission()
    {
        var application = Draft(Guid.NewGuid());
        var stranger = Citizen(Guid.NewGuid());

        var result = application.ApplyAs(Tax, "submit", stranger, null, Now);

        Assert.Equal(ApplicationActorRules.NotApplicant, result.Error!.Code);
        Assert.Equal("draft", application.State);
    }

    [Fact]
    public void Staff_cannot_apply_staff_transitions_to_their_own_application()
    {
        var userId = Guid.NewGuid();
        var application = Draft(userId);
        application.ApplyAs(Tax, "submit", Actor.Create(userId, [Roles.Citizen, Roles.Officer], mfaVerified: true), null, Now);

        var result = application.ApplyAs(
            Tax, "start_review", Actor.Create(userId, [Roles.Citizen, Roles.Officer], mfaVerified: true), null, Now);

        Assert.Equal(ApplicationActorRules.ConflictOfInterest, result.Error!.Code);
        Assert.Equal("submitted", application.State);
    }

    [Fact]
    public void A_missing_permission_is_reported_before_the_person_specific_rules()
    {
        var userId = Guid.NewGuid();
        var application = Draft(userId);
        application.ApplyAs(Tax, "submit", Citizen(userId), null, Now);

        var result = application.ApplyAs(Tax, "start_review", Citizen(userId), null, Now);

        Assert.Equal(WorkflowErrors.Forbidden, result.Error!.Code);
    }

    [Fact]
    public void Staff_can_review_other_peoples_applications_and_the_decision_is_recorded_against_them()
    {
        var citizenId = Guid.NewGuid();
        var officerId = Guid.NewGuid();
        var application = Draft(citizenId);
        application.ApplyAs(Tax, "submit", Citizen(citizenId), null, Now);

        var result = application.ApplyAs(Tax, "start_review", Staff(officerId, Roles.Officer), null, Now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(officerId, application.History[^1].ActorId);
    }

    [Fact]
    public void Available_transitions_follow_the_person_and_the_state()
    {
        var citizenId = Guid.NewGuid();
        var application = Draft(citizenId);

        Assert.Equal(["submit", "withdraw"], application.AvailableTransitions(Tax, Citizen(citizenId)).Order());
        Assert.Empty(application.AvailableTransitions(Tax, Staff(Guid.NewGuid(), Roles.Officer)));

        application.ApplyAs(Tax, "submit", Citizen(citizenId), null, Now);

        Assert.Equal(["start_review"], application.AvailableTransitions(Tax, Staff(Guid.NewGuid(), Roles.Officer)));
        Assert.Equal(["withdraw"], application.AvailableTransitions(Tax, Citizen(citizenId)));
        Assert.Empty(application.AvailableTransitions(Tax, Staff(citizenId, Roles.Officer)));
    }

    [Fact]
    public void The_latest_workflow_version_is_used_for_new_applications()
    {
        Assert.Same(BuiltInWorkflows.TaxFiling, BuiltInWorkflows.Latest(BuiltInWorkflows.TaxFilingCode));
        Assert.Null(BuiltInWorkflows.Latest("no-such-workflow"));
    }
}
