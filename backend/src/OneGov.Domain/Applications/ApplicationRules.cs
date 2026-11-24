using OneGov.Domain.Common;
using OneGov.Domain.Identity;
using OneGov.Domain.Workflow;

namespace OneGov.Domain.Applications;

/// <summary>
/// Rules about WHO may act on a particular application, on top of the workflow's permission check.
/// A role grants a kind of action; these rules tie it to the person.
/// </summary>
public static class ApplicationActorRules
{
    public const string NotApplicant = "application.not_applicant";
    public const string ConflictOfInterest = "application.conflict_of_interest";

    /// <summary>Returns the reason the actor may not apply the transition to this application, or null when they may.</summary>
    public static Error? Check(ServiceApplication application, WorkflowTransition transition, Guid actorId)
    {
        // Applicant actions (submit, provide documents, withdraw) belong to the applicant alone, even for staff
        // who also hold the citizen role and can see the application through their agency.
        if (transition.RequiredPermission == WorkflowPermissions.Submit && application.CitizenId != actorId)
        {
            return new Error(NotApplicant, "Only the person who applied can do this.");
        }

        // Staff actions never apply to the staff member's own application.
        if (Permissions.RequiresMfa(transition.RequiredPermission) && application.CitizenId == actorId)
        {
            return new Error(ConflictOfInterest, "Staff cannot work on their own application.");
        }

        return null;
    }
}
