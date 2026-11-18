namespace OneGov.Domain.Workflow;

/// <summary>Permission identifiers used by workflow transitions. Granted to roles in the identity module.</summary>
public static class WorkflowPermissions
{
    public const string Submit = "application:submit";
    public const string Review = "application:review";
    public const string Verify = "application:verify";
    public const string Decide = "application:decide";
}

/// <summary>
/// SAMPLE workflows for development. They do not encode official Ethiopian tax or national ID rules;
/// official requirements must be supplied by the responsible agencies before any real use.
/// </summary>
public static class BuiltInWorkflows
{
    public const string TaxFilingCode = "tax-filing.sample";
    public const string IdRenewalCode = "id-renewal.sample";

    public static WorkflowDefinition TaxFiling { get; } = new(
        TaxFilingCode,
        version: 1,
        initialState: "draft",
        transitions:
        [
            new("submit", ["draft"], "submitted", WorkflowPermissions.Submit),
            new("start_review", ["submitted"], "under_review", WorkflowPermissions.Review),
            new("request_documents", ["under_review"], "documents_requested", WorkflowPermissions.Review, RequiresNote: true),
            new("provide_documents", ["documents_requested"], "under_review", WorkflowPermissions.Submit),
            new("approve", ["under_review"], "approved", WorkflowPermissions.Decide),
            new("reject", ["under_review"], "rejected", WorkflowPermissions.Decide, RequiresNote: true),
            new("withdraw", ["draft", "submitted", "under_review", "documents_requested"], "withdrawn", WorkflowPermissions.Submit),
        ],
        editableStates: ["draft", "documents_requested"]);

    public static WorkflowDefinition IdRenewal { get; } = new(
        IdRenewalCode,
        version: 1,
        initialState: "draft",
        transitions:
        [
            new("submit", ["draft"], "submitted", WorkflowPermissions.Submit),
            new("start_review", ["submitted"], "under_review", WorkflowPermissions.Review),
            new("verify_identity", ["under_review"], "identity_verified", WorkflowPermissions.Verify),
            new("request_documents", ["under_review", "identity_verified"], "documents_requested", WorkflowPermissions.Review, RequiresNote: true),
            new("provide_documents", ["documents_requested"], "under_review", WorkflowPermissions.Submit),
            new("approve", ["identity_verified"], "approved", WorkflowPermissions.Decide),
            new("reject", ["under_review", "identity_verified"], "rejected", WorkflowPermissions.Decide, RequiresNote: true),
            new("withdraw", ["draft", "submitted", "under_review", "identity_verified", "documents_requested"], "withdrawn", WorkflowPermissions.Submit),
        ],
        editableStates: ["draft", "documents_requested"]);

    public static IReadOnlyList<WorkflowDefinition> All { get; } = [TaxFiling, IdRenewal];

    public static WorkflowDefinition? Find(string code, int version) =>
        All.FirstOrDefault(w => w.Code == code && w.Version == version);
}
