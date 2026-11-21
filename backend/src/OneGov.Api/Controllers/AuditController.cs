using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneGov.Api.Security;
using OneGov.Application.Audit;
using OneGov.Domain.Audit;

namespace OneGov.Api.Controllers;

/// <summary>Audit trail integrity. Requires the audit:read permission and a multi-factor sign-in.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/audit")]
[Produces("application/json")]
public sealed class AuditController(IAuditLog audit) : ControllerBase
{
    [HttpGet("verify")]
    [Authorize(Policy = PermissionPolicies.AuditRead)]
    [ProducesResponseType<AuditVerification>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Verify(CancellationToken cancellationToken) =>
        Ok(await audit.VerifyAsync(cancellationToken));
}
