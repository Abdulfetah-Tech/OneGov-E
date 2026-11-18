using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using OneGov.Application.Common;
using OneGov.Application.Registry;
using OneGov.Domain.Common;

namespace OneGov.Api.Controllers;

/// <summary>Public service directory (no authentication required).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/services")]
public sealed class ServicesController(ServiceDirectoryService directory) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<ServiceSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List([FromQuery] ListServicesQuery query, CancellationToken cancellationToken)
    {
        var result = await directory.ListAsync(query, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error!);
    }

    [HttpGet("{code}")]
    [ProducesResponseType<ServiceDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string code, [FromQuery] string language = "en", CancellationToken cancellationToken = default)
    {
        var result = await directory.GetAsync(code, language, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error!);
    }

    private ObjectResult ToProblem(Error error)
    {
        var status = error.Code == "registry.not_found"
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status400BadRequest;
        return Problem(detail: error.Message, statusCode: status, title: "Request could not be completed.", type: error.Code);
    }
}
