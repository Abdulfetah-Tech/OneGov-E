using Microsoft.EntityFrameworkCore;
using OneGov.Application.Audit;
using OneGov.Domain.Audit;
using OneGov.Infrastructure.Persistence;

namespace OneGov.Infrastructure.Services;

public sealed class AuditLog(OneGovDbContext db, TimeProvider timeProvider) : IAuditLog
{
    // Serialises appends across all API instances so the chain never forks.
    private const long AdvisoryLockKey = 727_001L;
    private const int VerifyBatchSize = 1000;

    public async Task<AuditEntry> AppendAsync(
        string actor, string action, string subject, string? purpose, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({AdvisoryLockKey})", cancellationToken);

        var previous = await db.AuditEntries
            .AsNoTracking()
            .OrderByDescending(e => e.Sequence)
            .FirstOrDefaultAsync(cancellationToken);

        var entry = AuditChain.CreateNext(previous, timeProvider.GetUtcNow(), actor, action, subject, purpose);
        db.AuditEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return entry;
    }

    public async Task<AuditVerification> VerifyAsync(CancellationToken cancellationToken)
    {
        var previousHash = AuditChain.GenesisHash;
        long nextSequence = 1;

        while (true)
        {
            var batch = await db.AuditEntries
                .AsNoTracking()
                .Where(e => e.Sequence >= nextSequence)
                .OrderBy(e => e.Sequence)
                .Take(VerifyBatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                return AuditVerification.Valid;
            }

            var result = AuditChain.Verify(batch, previousHash, nextSequence);
            if (!result.IsValid)
            {
                return result;
            }

            previousHash = batch[^1].Hash;
            nextSequence = batch[^1].Sequence + 1;
        }
    }
}
