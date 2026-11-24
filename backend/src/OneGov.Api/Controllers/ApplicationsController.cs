using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneGov.Api.Http;
using OneGov.Api.Security;
using OneGov.Application.Applications;
using OneGov.Application.Common;

namespace OneGov.Api.Controllers;

/// <summary>
/// Applications for government services and their progress. Citizens see their own; staff see their agency's after a
/// multi-factor sign-in. An application the caller may not see is reported as not found.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/applications")]
[Produces("application/json")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ApplicationsController(IApplicationService applications) : ControllerBase
{
    /// <summary>Starts a draft application for a service.</summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.ApplicationSubmit)]
    [ProducesResponseType<ApplicationDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateApplicationRequest request, CancellationToken cancellationToken)
    {
        if (User.ToActor() is not { } actor)
        {
            return Unauthorized();
        }

        var result = await applications.CreateDraftAsync(actor, request, cancellationToken);
        return result.IsSuccess
            ? Created($"/api/v1/applications/{result.Value.Id}", result.Value)
            : this.ToProblem(result.Error!);
    }

    /// <summary>The applications the caller may see, newest activity first. Staff can filter by <c>state</c>.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ApplicationSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] ListApplicationsQuery query, CancellationToken cancellationToken)
    {
        var result = await applications.ListAsync(query, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error!);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        if (User.ToActor() is not { } actor)
        {
            return Unauthorized();
        }

        var result = await applications.GetAsync(actor, id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error!);
    }

    /// <summary>Saves the form data of a draft (or of an application waiting for more information). Applicant only.</summary>
    [HttpPut("{id:guid}/payload")]
    [ProducesResponseType<ApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdatePayload(
        Guid id, [FromBody] UpdatePayloadRequest request, CancellationToken cancellationToken)
    {
        if (User.ToActor() is not { } actor)
        {
            return Unauthorized();
        }

        var result = await applications.UpdatePayloadAsync(actor, id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error!);
    }

    /// <summary>Moves the application along its workflow: submit, start_review, approve and so on. The response lists what can happen next.</summary>
    [HttpPost("{id:guid}/transitions")]
    [ProducesResponseType<ApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Transition(
        Guid id, [FromBody] TransitionRequest request, CancellationToken cancellationToken)
    {
        if (User.ToActor() is not { } actor)
        {
            return Unauthorized();
        }

        var result = await applications.ApplyTransitionAsync(actor, id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error!);
    }
}
