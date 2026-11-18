using OneGov.Domain.Workflow;

namespace OneGov.Domain.Tests;

public class WorkflowTests
{
    private static readonly HashSet<string> Citizen = [WorkflowPermissions.Submit];
    private static readonly HashSet<string> Officer = [WorkflowPermissions.Review, WorkflowPermissions.Verify];
    private static readonly HashSet<string> Decider = [WorkflowPermissions.Decide];

    private static readonly WorkflowDefinition Tax = BuiltInWorkflows.TaxFiling;

    [Fact]
    public void Citizen_can_submit_a_draft()
    {
        var result = WorkflowEngine.Evaluate(Tax, "draft", "submit", Citizen, null);

        Assert.True(result.IsSuccess);
        Assert.Equal("submitted", result.Value.ToState);
    }

    [Fact]
    public void Citizen_cannot_start_a_review()
    {
        var result = WorkflowEngine.Evaluate(Tax, "submitted", "start_review", Citizen, null);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowErrors.Forbidden, result.Error!.Code);
    }

    [Fact]
    public void Unknown_transition_is_reported()
    {
        var result = WorkflowEngine.Evaluate(Tax, "draft", "teleport", Citizen, null);

        Assert.Equal(WorkflowErrors.UnknownTransition, result.Error!.Code);
    }

    [Fact]
    public void Transition_from_the_wrong_state_is_rejected()
    {
        var result = WorkflowEngine.Evaluate(Tax, "draft", "approve", Decider, null);

        Assert.Equal(WorkflowErrors.InvalidState, result.Error!.Code);
    }

    [Theory]
    [InlineData("request_documents")]
    public void Officer_must_explain_a_documents_request(string transition)
    {
        Assert.Equal(WorkflowErrors.NoteRequired, WorkflowEngine.Evaluate(Tax, "under_review", transition, Officer, "  ").Error!.Code);
        Assert.True(WorkflowEngine.Evaluate(Tax, "under_review", transition, Officer, "Please upload the receipts.").IsSuccess);
    }

    [Fact]
    public void Rejection_needs_a_note()
    {
        Assert.Equal(WorkflowErrors.NoteRequired, WorkflowEngine.Evaluate(Tax, "under_review", "reject", Decider, null).Error!.Code);
        Assert.True(WorkflowEngine.Evaluate(Tax, "under_review", "reject", Decider, "Missing statement.").IsSuccess);
    }

    [Fact]
    public void Tax_workflow_has_the_expected_terminal_states() =>
        Assert.Equivalent(new[] { "approved", "rejected", "withdrawn" }, Tax.TerminalStates);

    [Fact]
    public void Id_renewal_cannot_be_approved_before_identity_is_verified()
    {
        var renewal = BuiltInWorkflows.IdRenewal;

        Assert.Equal(WorkflowErrors.InvalidState, WorkflowEngine.Evaluate(renewal, "under_review", "approve", Decider, null).Error!.Code);
        Assert.True(WorkflowEngine.Evaluate(renewal, "identity_verified", "approve", Decider, null).IsSuccess);
    }

    [Fact]
    public void Every_state_in_every_builtin_workflow_is_reachable_from_the_start()
    {
        foreach (var workflow in BuiltInWorkflows.All)
        {
            var reached = new HashSet<string> { workflow.InitialState };
            var queue = new Queue<string>([workflow.InitialState]);
            while (queue.Count > 0)
            {
                foreach (var transition in workflow.AvailableFrom(queue.Dequeue()))
                {
                    if (reached.Add(transition.ToState))
                    {
                        queue.Enqueue(transition.ToState);
                    }
                }
            }

            Assert.Equivalent(workflow.States, reached);
        }
    }

    [Fact]
    public void Terminal_states_offer_no_transitions()
    {
        foreach (var workflow in BuiltInWorkflows.All)
        {
            foreach (var terminal in workflow.TerminalStates)
            {
                Assert.Empty(workflow.AvailableFrom(terminal));
            }
        }
    }

    [Fact]
    public void Invalid_definitions_are_rejected()
    {
        WorkflowTransition[] duplicate =
        [
            new("go", ["a"], "b", "p"),
            new("go", ["b"], "c", "p"),
        ];

        Assert.Throws<ArgumentException>(() => new WorkflowDefinition("x", 1, "a", duplicate, []));
        Assert.Throws<ArgumentException>(() => new WorkflowDefinition("x", 1, "a", [new("go", ["a"], "b", "p")], ["nowhere"]));
        Assert.Throws<ArgumentException>(() => new WorkflowDefinition("x", 1, "a", [], []));
    }
}
