using OneGov.Domain.Common;

namespace OneGov.Domain.Workflow;

public static class WorkflowErrors
{
    public const string UnknownTransition = "workflow.unknown_transition";
    public const string InvalidState = "workflow.invalid_state";
    public const string Forbidden = "workflow.forbidden";
    public const string NoteRequired = "workflow.note_required";
}

/// <summary>Pure, side-effect-free evaluation of a transition request.</summary>
public static class WorkflowEngine
{
    public static Result<WorkflowTransition> Evaluate(
        WorkflowDefinition definition,
        string currentState,
        string transitionName,
        IReadOnlySet<string> actorPermissions,
        string? note)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(actorPermissions);

        var transition = definition.Transitions.FirstOrDefault(t => t.Name == transitionName);
        if (transition is null)
        {
            return Result<WorkflowTransition>.Failure(
                WorkflowErrors.UnknownTransition, $"Unknown transition '{transitionName}'.");
        }

        if (!transition.FromStates.Contains(currentState))
        {
            return Result<WorkflowTransition>.Failure(
                WorkflowErrors.InvalidState,
                $"Transition '{transitionName}' is not allowed from state '{currentState}'.");
        }

        if (!actorPermissions.Contains(transition.RequiredPermission))
        {
            return Result<WorkflowTransition>.Failure(
                WorkflowErrors.Forbidden, $"Transition '{transitionName}' requires a permission the actor does not hold.");
        }

        if (transition.RequiresNote && string.IsNullOrWhiteSpace(note))
        {
            return Result<WorkflowTransition>.Failure(
                WorkflowErrors.NoteRequired, $"Transition '{transitionName}' requires a note.");
        }

        return Result<WorkflowTransition>.Success(transition);
    }
}
