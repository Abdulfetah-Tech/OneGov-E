namespace OneGov.Domain.Workflow;

/// <summary>One named, permission-guarded move between states.</summary>
public sealed record WorkflowTransition(
    string Name,
    IReadOnlyList<string> FromStates,
    string ToState,
    string RequiredPermission,
    bool RequiresNote = false);

/// <summary>
/// A versioned, declarative state machine. Services (tax filing, ID renewal, ...) are configurations of this
/// engine rather than separate code paths. Invalid definitions are programmer errors and throw on construction.
/// </summary>
public sealed class WorkflowDefinition
{
    public WorkflowDefinition(
        string code,
        int version,
        string initialState,
        IReadOnlyList<WorkflowTransition> transitions,
        IReadOnlyCollection<string> editableStates)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(initialState);
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        ArgumentNullException.ThrowIfNull(transitions);
        ArgumentNullException.ThrowIfNull(editableStates);

        if (transitions.Count == 0)
        {
            throw new ArgumentException("A workflow needs at least one transition.", nameof(transitions));
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        var states = new HashSet<string>(StringComparer.Ordinal) { initialState };
        foreach (var transition in transitions)
        {
            if (!names.Add(transition.Name))
            {
                throw new ArgumentException($"Duplicate transition name '{transition.Name}'.", nameof(transitions));
            }

            if (transition.FromStates.Count == 0)
            {
                throw new ArgumentException($"Transition '{transition.Name}' has no source states.", nameof(transitions));
            }

            states.UnionWith(transition.FromStates);
            states.Add(transition.ToState);
        }

        foreach (var editable in editableStates)
        {
            if (!states.Contains(editable))
            {
                throw new ArgumentException($"Editable state '{editable}' is not part of the workflow.", nameof(editableStates));
            }
        }

        var sources = transitions.SelectMany(t => t.FromStates).ToHashSet(StringComparer.Ordinal);
        Code = code;
        Version = version;
        InitialState = initialState;
        Transitions = transitions;
        EditableStates = editableStates.ToHashSet(StringComparer.Ordinal);
        States = states;
        TerminalStates = states.Where(s => !sources.Contains(s)).ToHashSet(StringComparer.Ordinal);
    }

    public string Code { get; }

    public int Version { get; }

    public string InitialState { get; }

    public IReadOnlyList<WorkflowTransition> Transitions { get; }

    /// <summary>States in which the applicant may change the submitted data.</summary>
    public IReadOnlySet<string> EditableStates { get; }

    public IReadOnlySet<string> States { get; }

    /// <summary>States with no outgoing transition (derived).</summary>
    public IReadOnlySet<string> TerminalStates { get; }

    public IEnumerable<WorkflowTransition> AvailableFrom(string state) =>
        Transitions.Where(t => t.FromStates.Contains(state));
}
