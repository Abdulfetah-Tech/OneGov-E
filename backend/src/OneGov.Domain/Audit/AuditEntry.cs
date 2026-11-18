namespace OneGov.Domain.Audit;

/// <summary>
/// An immutable audit record that commits to its predecessor's hash, forming a tamper-evident chain.
/// Timestamps are stored as Unix milliseconds so the hash survives database round-trips exactly.
/// </summary>
public sealed class AuditEntry(
    long sequence,
    long occurredAtUnixMs,
    string actor,
    string action,
    string subject,
    string purpose,
    string previousHash,
    string hash)
{
    public long Sequence { get; private set; } = sequence;

    public long OccurredAtUnixMs { get; private set; } = occurredAtUnixMs;

    public string Actor { get; private set; } = actor;

    public string Action { get; private set; } = action;

    public string Subject { get; private set; } = subject;

    /// <summary>Why the data was accessed (empty when not applicable).</summary>
    public string Purpose { get; private set; } = purpose;

    public string PreviousHash { get; private set; } = previousHash;

    public string Hash { get; private set; } = hash;

    public DateTimeOffset OccurredAt => DateTimeOffset.FromUnixTimeMilliseconds(OccurredAtUnixMs);
}
