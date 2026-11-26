using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneGov.Api.Http;
using OneGov.Application.Documents;
using OneGov.Domain.Documents;

namespace OneGov.Api.Controllers;

/// <summary>
/// Supporting documents of an application. Visible to whoever can see the application. Only the applicant can add or remove
/// them while the application is open for changes. Files are only ever sent as downloads.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/applications/{applicationId:guid}/documents")]
[Produces("application/json")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DocumentsController(IDocumentService documents) : ControllerBase
{
    // The limit on the request leaves room for the multipart envelope around the file.
    private const long RequestLimit = DocumentRules.MaxBytes + 512 * 1024;

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DocumentDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid applicationId, CancellationToken cancellationToken)
    {
        var result = await documents.ListAsync(applicationId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error!);
    }

    /// <summary>Uploads one PDF, PNG or JPEG as the form field <c>file</c>.</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(RequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = RequestLimit)]
    [ProducesResponseType<DocumentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> Upload(Guid applicationId, IFormFile file, CancellationToken cancellationToken)
    {
        if (User.ToActor() is not { } actor)
        {
            return Unauthorized();
        }

        await using var content = file.OpenReadStream();
        var result = await documents.UploadAsync(actor, applicationId, file.FileName, content, cancellationToken);
        return result.IsSuccess
            ? Created($"/api/v1/applications/{applicationId}/documents/{result.Value.Id}", result.Value)
            : this.ToProblem(result.Error!);
    }

    [HttpGet("{documentId:guid}")]
    [Produces("application/octet-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid applicationId, Guid documentId, CancellationToken cancellationToken)
    {
        if (User.ToActor() is not { } actor)
        {
            return Unauthorized();
        }

        var result = await documents.DownloadAsync(actor, applicationId, documentId, cancellationToken);
        if (!result.IsSuccess)
        {
            return this.ToProblem(result.Error!);
        }

        // Always an attachment: the browser saves the file and never renders it inside our site.
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(result.Value.Stream, result.Value.ContentType, result.Value.FileName);
    }

    [HttpDelete("{documentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid applicationId, Guid documentId, CancellationToken cancellationToken)
    {
        if (User.ToActor() is not { } actor)
        {
            return Unauthorized();
        }

        var result = await documents.DeleteAsync(actor, applicationId, documentId, cancellationToken);
        return result.IsSuccess ? NoContent() : this.ToProblem(result.Error!);
    }
}
