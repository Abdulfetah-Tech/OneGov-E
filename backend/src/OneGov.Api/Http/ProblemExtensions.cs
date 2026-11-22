using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using OneGov.Application.Identity;
using OneGov.Domain.Common;

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
            AuthErrors.NationalIdFailed => StatusCodes.Status401Unauthorized,
            AuthErrors.NationalIdUnavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status400BadRequest,
        };

        return controller.Problem(detail: error.Message, statusCode: status, title: "Request could not be completed.", type: error.Code);
    }

    /// <summary>The signed-in user's id from the access token's <c>sub</c> claim.</summary>
    public static Guid? GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("sub")?.Value, out var id) ? id : null;
}
