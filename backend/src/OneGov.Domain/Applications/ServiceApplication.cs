using OneGov.Domain.Common;
using OneGov.Domain.Identity;
using OneGov.Domain.Workflow;

namespace OneGov.Domain.Applications;

public sealed class ApplicationHistoryEntry : Entity
{
    private ApplicationHistoryEntry()
    {
    }

    internal ApplicationHistoryEntry(
        Guid applicationId, string transition, string fromState, string toState, Guid actorId, string? note, DateTimeOffset occurredAt)
    {
        ApplicationId = applicationId;
        Transition = transition;
        FromState = fromState;
        ToState = toState;
        ActorId = actorId;
        Note = note;
        OccurredAt = occurredAt;
    }

    public Guid ApplicationId { get; private set; }

    public string Transition { get; private set; } = string.Empty;

    public string FromState { get; private set; } = string.Empty;

    public string ToState { get; private set; } = string.Empty;

    public Guid ActorId { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
}

/// <summary>A citizen's application for a government service. State changes go through the workflow engine only.</summary>
public sealed class ServiceApplication : AuditableEntity
{
    private readonly List<ApplicationHistoryEntry> _history = [];

    private ServiceApplication()
    {
    }

    public Guid ServiceId { get; private set; }

    public Guid AgencyId { get; private set; }

    public Guid CitizenId { get; private set; }

    public string WorkflowCode { get; private set; } = string.Empty;

    public int WorkflowVersion { get; private set; }

    public string State { get; private set; } = string.Empty;

    public string? ReferenceNumber { get; private set; }

    /// <summary>Form data as JSON. Validated by the service's schema before it reaches the domain.</summary>
    public string PayloadJson { get; private set; } = "{}";

    public IReadOnlyList<ApplicationHistoryEntry> History => _history;

    public static ServiceApplication CreateDraft(Guid serviceId, Guid agencyId, Guid citizenId, WorkflowDefinition workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        return new ServiceApplication
        {
            ServiceId = serviceId,
            AgencyId = agencyId,
            CitizenId = citizenId,
            WorkflowCode = workflow.Code,
            WorkflowVersion = workflow.Version,
            State = workflow.InitialState,
        };
    }

    public void AssignReference(string referenceNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceNumber);
        if (ReferenceNumber is not null)
        {
            throw new InvalidOperationException("A reference number is assigned once.");
        }

        ReferenceNumber = referenceNumber;
    }

    public Result<bool> UpdatePayload(string payloadJson, WorkflowDefinition workflow)
    {
        ArgumentException.ThrowIfNullOrEmpty(payloadJson);
        var mismatch = EnsureWorkflowMatches(workflow);
        if (mismatch is not null)
        {
            return Result<bool>.Failure(mismatch);
        }

        if (!workflow.EditableStates.Contains(State))
        {
            return Result<bool>.Failure("application.not_editable", $"The application cannot be edited in state '{State}'.");
        }

        PayloadJson = payloadJson;
        return Result<bool>.Success(true);
    }

    public Result<ApplicationHistoryEntry> Apply(
        WorkflowDefinition workflow,
        string transitionName,
        Guid actorId,
        IReadOnlySet<string> actorPermissions,
        string? note,
        DateTimeOffset now)
    {
        var mismatch = EnsureWorkflowMatches(workflow);
        if (mismatch is not null)
        {
            return Result<ApplicationHistoryEntry>.Failure(mismatch);
        }

        var evaluation = WorkflowEngine.Evaluate(workflow, State, transitionName, actorPermissions, note);
        if (!evaluation.IsSuccess)
        {
            return Result<ApplicationHistoryEntry>.Failure(evaluation.Error!);
        }

        var transition = evaluation.Value;
        var entry = new ApplicationHistoryEntry(Id, transition.Name, State, transition.ToState, actorId, note?.Trim(), now);
        _history.Add(entry);
        State = transition.ToState;
        return Result<ApplicationHistoryEntry>.Success(entry);
    }

    /// <summary>
    /// Applies a transition on behalf of a person. Checks the workflow's permission first, then the rules about who may act on
    /// this particular application. Services must use this rather than <see cref="Apply"/>, which does not look at who the actor is.
    /// </summary>
    public Result<ApplicationHistoryEntry> ApplyAs(
        WorkflowDefinition workflow, string transitionName, Actor actor, string? note, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var mismatch = EnsureWorkflowMatches(workflow);
        if (mismatch is not null)
        {
            return Result<ApplicationHistoryEntry>.Failure(mismatch);
        }

        var evaluation = WorkflowEngine.Evaluate(workflow, State, transitionName, actor.Permissions, note);
        if (!evaluation.IsSuccess)
        {
            return Result<ApplicationHistoryEntry>.Failure(evaluation.Error!);
        }

        var violation = ApplicationActorRules.Check(this, evaluation.Value, actor.UserId);
        return violation is not null
            ? Result<ApplicationHistoryEntry>.Failure(violation)
            : Apply(workflow, transitionName, actor.UserId, actor.Permissions, note, now);
    }

    /// <summary>The transitions this person could apply right now, for showing the right buttons.</summary>
    public IReadOnlyList<string> AvailableTransitions(WorkflowDefinition workflow, Actor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (EnsureWorkflowMatches(workflow) is not null)
        {
            return [];
        }

        return workflow.Transitions
            .Where(t => t.FromStates.Contains(State)
                && actor.Has(t.RequiredPermission)
                && ApplicationActorRules.Check(this, t, actor.UserId) is null)
            .Select(t => t.Name)
            .ToList();
    }

    private Error? EnsureWorkflowMatches(WorkflowDefinition workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        return workflow.Code == WorkflowCode && workflow.Version == WorkflowVersion
            ? null
            : new Error("application.workflow_mismatch", "The workflow does not match the one this application started with.");
    }
}
