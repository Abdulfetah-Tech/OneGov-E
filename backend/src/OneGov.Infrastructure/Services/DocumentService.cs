using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using OneGov.Application.Applications;
using OneGov.Application.Audit;
using OneGov.Application.Documents;
using OneGov.Domain.Applications;
using OneGov.Domain.Common;
using OneGov.Domain.Documents;
using OneGov.Domain.Identity;
using OneGov.Domain.Workflow;
using OneGov.Infrastructure.Persistence;

namespace OneGov.Infrastructure.Services;

public sealed class DocumentService(
    OneGovDbContext db, IDocumentStore store, IAuditLog audit, TimeProvider timeProvider) : IDocumentService
{
    public async Task<Result<DocumentDto>> UploadAsync(
        Actor actor, Guid applicationId, string? fileName, Stream content, CancellationToken cancellationToken)
    {
        var application = await FindVisibleAsync(applicationId, cancellationToken);
        if (application is null)
        {
            return NotFound<DocumentDto>();
        }

        var denied = CheckApplicantMayChange(application, actor);
        if (denied is not null)
        {
            return Result<DocumentDto>.Failure(denied);
        }

        var existing = await db.ApplicationDocuments.CountAsync(d => d.ApplicationId == applicationId, cancellationToken);
        if (existing >= DocumentRules.MaxPerApplication)
        {
            return Result<DocumentDto>.Failure(
                DocumentErrors.LimitReached, $"An application can have at most {DocumentRules.MaxPerApplication} documents.");
        }

        // Read at most one byte more than the limit, so an oversized upload is noticed without holding all of it.
        var buffer = new byte[DocumentRules.MaxBytes + 1];
        var length = 0;
        int read;
        while (length < buffer.Length && (read = await content.ReadAsync(buffer.AsMemory(length), cancellationToken)) > 0)
        {
            length += read;
        }

        if (length > DocumentRules.MaxBytes)
        {
            return Result<DocumentDto>.Failure(
                DocumentErrors.TooLarge, $"Files can be at most {DocumentRules.MaxBytes / (1024 * 1024)} MB.");
        }

        var detected = length == 0 ? null : DocumentRules.Detect(buffer.AsSpan(0, length));
        if (detected is null)
        {
            return Result<DocumentDto>.Failure(DocumentErrors.InvalidFile, "Only PDF, PNG and JPEG files can be uploaded.");
        }

        var data = buffer.AsMemory(0, length);
        var key = Guid.NewGuid().ToString("N");
        try
        {
            await store.SaveAsync(key, data, cancellationToken);
        }
        catch (IOException)
        {
            return Result<DocumentDto>.Failure(DocumentErrors.StorageFailed, "The file could not be stored. Please try again.");
        }

        var document = ApplicationDocument.Create(
            applicationId,
            actor.UserId,
            DocumentRules.DisplayName(fileName, detected.Extension),
            detected.ContentType,
            length,
            Convert.ToHexStringLower(SHA256.HashData(data.Span)),
            key,
            timeProvider.GetUtcNow());

        try
        {
            db.ApplicationDocuments.Add(document);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await store.DeleteAsync(key, CancellationToken.None);
            throw;
        }

        await AuditAsync(actor, "application.document_uploaded", applicationId, cancellationToken);
        return Result<DocumentDto>.Success(ToDto(document));
    }

    public async Task<Result<IReadOnlyList<DocumentDto>>> ListAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        if (await FindVisibleAsync(applicationId, cancellationToken) is null)
        {
            return NotFound<IReadOnlyList<DocumentDto>>();
        }

        var documents = await db.ApplicationDocuments.AsNoTracking()
            .Where(d => d.ApplicationId == applicationId)
            .OrderBy(d => d.CreatedAt).ThenBy(d => d.Id)
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<DocumentDto>>.Success(documents.Select(ToDto).ToList());
    }

    public async Task<Result<DocumentContent>> DownloadAsync(
        Actor actor, Guid applicationId, Guid documentId, CancellationToken cancellationToken)
    {
        if (await FindVisibleAsync(applicationId, cancellationToken) is null)
        {
            return NotFound<DocumentContent>();
        }

        var document = await db.ApplicationDocuments.AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == documentId && d.ApplicationId == applicationId, cancellationToken);
        var stream = document is null ? null : await store.OpenReadAsync(document.StorageKey, cancellationToken);
        if (document is null || stream is null)
        {
            return NotFound<DocumentContent>();
        }

        // Reading someone's documents is itself a recorded event.
        await AuditAsync(actor, "application.document_downloaded", applicationId, cancellationToken);
        return Result<DocumentContent>.Success(new DocumentContent(stream, document.ContentType, document.FileName));
    }

    public async Task<Result<bool>> DeleteAsync(Actor actor, Guid applicationId, Guid documentId, CancellationToken cancellationToken)
    {
        var application = await FindVisibleAsync(applicationId, cancellationToken);
        if (application is null)
        {
            return NotFound<bool>();
        }

        var denied = CheckApplicantMayChange(application, actor);
        if (denied is not null)
        {
            return Result<bool>.Failure(denied);
        }

        var document = await db.ApplicationDocuments
            .SingleOrDefaultAsync(d => d.Id == documentId && d.ApplicationId == applicationId, cancellationToken);
        if (document is null)
        {
            return NotFound<bool>();
        }

        document.Delete(timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        await store.DeleteAsync(document.StorageKey, cancellationToken);
        await AuditAsync(actor, "application.document_deleted", applicationId, cancellationToken);
        return Result<bool>.Success(true);
    }

    /// <summary>The data scope filter on the context means an application the caller may not see is simply not found.</summary>
    private Task<ServiceApplication?> FindVisibleAsync(Guid applicationId, CancellationToken cancellationToken) =>
        db.Applications.AsNoTracking().SingleOrDefaultAsync(a => a.Id == applicationId, cancellationToken);

    private static Error? CheckApplicantMayChange(ServiceApplication application, Actor actor)
    {
        if (application.CitizenId != actor.UserId || !actor.Has(Permissions.ApplicationSubmit))
        {
            return new Error(ApplicationActorRules.NotApplicant, "Only the person who applied can do this.");
        }

        var workflow = BuiltInWorkflows.Find(application.WorkflowCode, application.WorkflowVersion);
        if (workflow is null)
        {
            return new Error(ApplicationErrors.WorkflowMissing, "The workflow for this application is not available.");
        }

        return workflow.EditableStates.Contains(application.State)
            ? null
            : new Error("application.not_editable", "This application can no longer be changed.");
    }

    private Task AuditAsync(Actor actor, string action, Guid applicationId, CancellationToken cancellationToken) =>
        audit.AppendAsync(actor.UserId.ToString(), action, $"application:{applicationId}", null, cancellationToken);

    private static DocumentDto ToDto(ApplicationDocument d) => new(d.Id, d.FileName, d.ContentType, d.SizeBytes, d.CreatedAt);

    private static Result<T> NotFound<T>() => Result<T>.Failure(DocumentErrors.NotFound, "Not found.");
}
