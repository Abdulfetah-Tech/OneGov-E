using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OneGov.Domain.Audit;

public sealed record AuditVerification(bool IsValid, long? FirstBrokenSequence, string? Reason)
{
    public static AuditVerification Valid { get; } = new(true, null, null);
}

/// <summary>Builds and verifies the hash chain. Contains no I/O.</summary>
public static class AuditChain
{
    /// <summary>Hash that precedes the first entry.</summary>
    public static readonly string GenesisHash = new('0', 64);

    public static AuditEntry CreateNext(
        AuditEntry? previous,
        DateTimeOffset occurredAt,
        string actor,
        string action,
        string subject,
        string? purpose)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        var sequence = previous is null ? 1L : previous.Sequence + 1;
        var previousHash = previous?.Hash ?? GenesisHash;
        var milliseconds = occurredAt.ToUnixTimeMilliseconds();
        var normalizedPurpose = purpose ?? string.Empty;
        var hash = ComputeHash(sequence, milliseconds, actor, action, subject, normalizedPurpose, previousHash);
        return new AuditEntry(sequence, milliseconds, actor, action, subject, normalizedPurpose, previousHash, hash);
    }

    public static string ComputeHash(
        long sequence,
        long occurredAtUnixMs,
        string actor,
        string action,
        string subject,
        string purpose,
        string previousHash)
    {
        // Length-prefixed fields: no delimiter can be forged by crafting field contents.
        var canonical = new StringBuilder()
            .Append(sequence.ToString(CultureInfo.InvariantCulture)).Append('|')
            .Append(occurredAtUnixMs.ToString(CultureInfo.InvariantCulture)).Append('|')
            .Append(Field(actor)).Append(Field(action)).Append(Field(subject)).Append(Field(purpose))
            .Append(previousHash)
            .ToString();

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>Verifies entries ordered by sequence. Pass a window that starts at the genesis entry or supply its predecessor hash.</summary>
    public static AuditVerification Verify(IEnumerable<AuditEntry> entries, string? expectedPreviousHash = null, long? expectedFirstSequence = null)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var previousHash = expectedPreviousHash ?? GenesisHash;
        var expectedSequence = expectedFirstSequence ?? 1L;

        foreach (var entry in entries)
        {
            if (entry.Sequence != expectedSequence)
            {
                return new AuditVerification(false, entry.Sequence, $"Expected sequence {expectedSequence}.");
            }

            if (!string.Equals(entry.PreviousHash, previousHash, StringComparison.Ordinal))
            {
                return new AuditVerification(false, entry.Sequence, "Previous hash does not match the preceding entry.");
            }

            var recomputed = ComputeHash(
                entry.Sequence, entry.OccurredAtUnixMs, entry.Actor, entry.Action, entry.Subject, entry.Purpose, entry.PreviousHash);
            if (!string.Equals(entry.Hash, recomputed, StringComparison.Ordinal))
            {
                return new AuditVerification(false, entry.Sequence, "Entry content does not match its hash.");
            }

            previousHash = entry.Hash;
            expectedSequence++;
        }

        return AuditVerification.Valid;
    }

    private static string Field(string value) =>
        string.Create(CultureInfo.InvariantCulture, $"{Encoding.UTF8.GetByteCount(value)}:{value}|");
}
