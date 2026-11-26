using OneGov.Domain.Common;
using OneGov.Domain.Identity;

namespace OneGov.Application.Documents;

public static class DocumentErrors
{
    public const string NotFound = "document.not_found";
    public const string InvalidFile = "document.invalid_file";
    public const string TooLarge = "document.too_large";
    public const string LimitReached = "document.limit_reached";
    public const string StorageFailed = "document.storage_failed";
}

public sealed record DocumentDto(Guid Id, string FileName, string ContentType, long SizeBytes, DateTimeOffset UploadedAt);

/// <summary>A file to send to the person. The caller disposes the stream.</summary>
public sealed record DocumentContent(Stream Stream, string ContentType, string FileName);

/// <summary>Where uploaded files are kept. Keys are opaque, random and chosen by the system.</summary>
public interface IDocumentStore
{
    Task SaveAsync(string key, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);

    /// <summary>Null when there is no such file.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

/// <summary>
/// Supporting documents for an application. Anyone who can see the application can list and download its documents; only the
/// applicant can add or remove them, and only while the application is open for changes.
/// </summary>
public interface IDocumentService
{
    Task<Result<DocumentDto>> UploadAsync(
        Actor actor, Guid applicationId, string? fileName, Stream content, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<DocumentDto>>> ListAsync(Guid applicationId, CancellationToken cancellationToken);

    Task<Result<DocumentContent>> DownloadAsync(
        Actor actor, Guid applicationId, Guid documentId, CancellationToken cancellationToken);

    Task<Result<bool>> DeleteAsync(Actor actor, Guid applicationId, Guid documentId, CancellationToken cancellationToken);
}
