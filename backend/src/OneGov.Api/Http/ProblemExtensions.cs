using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using OneGov.Application.Applications;
using OneGov.Application.Documents;
using OneGov.Application.Identity;
using OneGov.Application.Notifications;
using OneGov.Domain.Applications;
using OneGov.Domain.Common;
using OneGov.Domain.Identity;
using OneGov.Domain.Workflow;

namespace OneGov.Api.Http;

internal static class ProblemExtensions
{
    /// <summary>Maps a domain error to an RFC 9457 problem response. The error code is carried in <c>type</c>.</summary>
    public static ObjectResult ToProblem(this ControllerBase controller, Error error)
    {
        var status = error.Code switch
        {
            AuthErrors.InvalidCredentials or AuthErrors.InvalidToken or AuthErrors.MfaRequired => StatusCodes.Status401Unauthorized,
            AuthErrors.EmailTaken or AuthErrors.MfaAlreadyEnabled => StatusCodes.Status409Conflict,
            AuthErrors.ReauthenticationFailed => StatusCodes.Status403Forbidden,
            AuthErrors.NotFound => StatusCodes.Status404NotFound,
            ApplicationErrors.NotFound or ApplicationErrors.ServiceNotFound or NotificationErrors.NotFound
                or DocumentErrors.NotFound => StatusCodes.Status404NotFound,
            DocumentErrors.TooLarge => StatusCodes.Status413PayloadTooLarge,
            DocumentErrors.StorageFailed => StatusCodes.Status503ServiceUnavailable,
            ApplicationErrors.Forbidden or WorkflowErrors.Forbidden or ApplicationActorRules.NotApplicant
                or ApplicationActorRules.ConflictOfInterest => StatusCodes.Status403Forbidden,
            WorkflowErrors.InvalidState or ApplicationErrors.Conflict or ApplicationErrors.ServiceUnavailable
                or DocumentErrors.LimitReached
                or "application.not_editable" => StatusCodes.Status409Conflict,
            ApplicationErrors.WorkflowMissing or "application.workflow_mismatch" => StatusCodes.Status500InternalServerError,
            AuthErrors.NationalIdFailed => StatusCodes.Status401Unauthorized,
            AuthErrors.NationalIdUnavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status400BadRequest,
        };

        return controller.Problem(detail: error.Message, statusCode: status, title: "Request could not be completed.", type: error.Code);
    }

    /// <summary>Who is acting and what they may do, from the access token. Null when the token has no user id.</summary>
    public static Actor? ToActor(this ClaimsPrincipal user) =>
        user.GetUserId() is { } id
            ? Actor.Create(id, user.FindAll("role").Select(claim => claim.Value), user.HasClaim("amr", "otp"))
            : null;

    /// <summary>The signed-in user's id from the access token's <c>sub</c> claim.</summary>
    public static Guid? GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("sub")?.Value, out var id) ? id : null;
}
