using OneGov.Domain.Applications;
using OneGov.Domain.Workflow;

namespace OneGov.Domain.Tests;

public class ServiceApplicationTests
{
    private static readonly WorkflowDefinition Tax = BuiltInWorkflows.TaxFiling;
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);
    private static readonly HashSet<string> Citizen = [WorkflowPermissions.Submit];
    private static readonly HashSet<string> Officer = [WorkflowPermissions.Review];
    private static readonly HashSet<string> Decider = [WorkflowPermissions.Decide];

    private static ServiceApplication NewDraft() =>
        ServiceApplication.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Tax);

    [Fact]
    public void New_application_starts_in_the_initial_state()
    {
        var application = NewDraft();

        Assert.Equal("draft", application.State);
        Assert.Equal(Tax.Code, application.WorkflowCode);
        Assert.Empty(application.History);
    }

    [Fact]
    public void Happy_path_records_every_step()
    {
        var application = NewDraft();
        var citizenId = Guid.NewGuid();
        var officerId = Guid.NewGuid();

        Assert.True(application.Apply(Tax, "submit", citizenId, Citizen, null, Now).IsSuccess);
        Assert.True(application.Apply(Tax, "start_review", officerId, Officer, null, Now.AddHours(1)).IsSuccess);
        Assert.True(application.Apply(Tax, "approve", officerId, Decider, "Looks complete.", Now.AddHours(2)).IsSuccess);

        Assert.Equal("approved", application.State);
        Assert.Equal(3, application.History.Count);
        Assert.Equal("draft", application.History[0].FromState);
        Assert.Equal("submitted", application.History[0].ToState);
        Assert.Equal(citizenId, application.History[0].ActorId);
        Assert.Equal("Looks complete.", application.History[2].Note);
    }

    [Fact]
    public void Failed_transition_changes_nothing()
    {
        var application = NewDraft();

        var result = application.Apply(Tax, "approve", Guid.NewGuid(), Decider, null, Now);

        Assert.False(result.IsSuccess);
        Assert.Equal("draft", application.State);
        Assert.Empty(application.History);
    }

    [Fact]
    public void Payload_is_editable_only_in_editable_states()
    {
        var application = NewDraft();
        Assert.True(application.UpdatePayload("{\"income\":100}", Tax).IsSuccess);

        application.Apply(Tax, "submit", Guid.NewGuid(), Citizen, null, Now);
        var blocked = application.UpdatePayload("{\"income\":999}", Tax);

        Assert.False(blocked.IsSuccess);
        Assert.Equal("application.not_editable", blocked.Error!.Code);
        Assert.Equal("{\"income\":100}", application.PayloadJson);
    }

    [Fact]
    public void Applicant_can_edit_again_after_documents_are_requested()
    {
        var application = NewDraft();
        application.Apply(Tax, "submit", Guid.NewGuid(), Citizen, null, Now);
        application.Apply(Tax, "start_review", Guid.NewGuid(), Officer, null, Now);
        application.Apply(Tax, "request_documents", Guid.NewGuid(), Officer, "Add receipts.", Now);

        Assert.True(application.UpdatePayload("{\"receipts\":3}", Tax).IsSuccess);
    }

    [Fact]
    public void A_different_workflow_is_refused()
    {
        var application = NewDraft();

        var result = application.Apply(BuiltInWorkflows.IdRenewal, "submit", Guid.NewGuid(), Citizen, null, Now);

        Assert.Equal("application.workflow_mismatch", result.Error!.Code);
    }

    [Fact]
    public void Reference_number_can_only_be_assigned_once()
    {
        var application = NewDraft();
        application.AssignReference(ReferenceNumber.Format("TAX-FILING", 2026, 1));

        Assert.Throws<InvalidOperationException>(() => application.AssignReference("OG-OTHER-2026-000002"));
    }
}
