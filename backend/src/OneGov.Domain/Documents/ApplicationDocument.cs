using OneGov.Domain.Common;

namespace OneGov.Domain.Documents;

/// <summary>
/// Metadata of a file attached to an application. The file itself lives in document storage under <see cref="StorageKey"/>,
/// an opaque random value, so nothing a user supplies ever becomes part of a path on disk.
/// </summary>
public sealed class ApplicationDocument : Entity
{
    private ApplicationDocument()
    {
    }

    public Guid ApplicationId { get; private set; }

    public Guid UploadedByUserId { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    /// <summary>SHA-256 of the content, in lowercase hex, to detect later changes to the stored file.</summary>
    public string Sha256 { get; private set; } = string.Empty;

    public string StorageKey { get; private set; } = string.Empty;

    /// <summary>Always "not_scanned" until malware scanning is connected. Files are only ever served as downloads, never displayed inline.</summary>
    public string ScanStatus { get; private set; } = "not_scanned";

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public static ApplicationDocument Create(
        Guid applicationId, Guid uploadedByUserId, string fileName, string contentType, long sizeBytes, string sha256,
        string storageKey, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeBytes);

        return new ApplicationDocument
        {
            ApplicationId = applicationId,
            UploadedByUserId = uploadedByUserId,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            Sha256 = sha256,
            StorageKey = storageKey,
            CreatedAt = now,
        };
    }

    public void Delete(DateTimeOffset now) => DeletedAt ??= now;
}
