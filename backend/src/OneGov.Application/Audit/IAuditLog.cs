using OneGov.Domain.Audit;

namespace OneGov.Application.Audit;

/// <summary>Append-only, hash-chained audit trail.</summary>
public interface IAuditLog
{
    Task<AuditEntry> AppendAsync(string actor, string action, string subject, string? purpose, CancellationToken cancellationToken);

    Task<AuditVerification> VerifyAsync(CancellationToken cancellationToken);
}
