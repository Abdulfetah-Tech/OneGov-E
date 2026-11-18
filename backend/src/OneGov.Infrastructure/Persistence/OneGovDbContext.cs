using Microsoft.EntityFrameworkCore;
using OneGov.Domain.Applications;
using OneGov.Domain.Audit;
using OneGov.Domain.Common;
using OneGov.Domain.Registry;

namespace OneGov.Infrastructure.Persistence;

public sealed class OneGovDbContext(DbContextOptions<OneGovDbContext> options, TimeProvider timeProvider)
    : DbContext(options)
{
    public DbSet<Agency> Agencies => Set<Agency>();

    public DbSet<GovService> Services => Set<GovService>();

    public DbSet<ServiceApplication> Applications => Set<ServiceApplication>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OneGovDbContext).Assembly);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyPersistenceRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyPersistenceRules();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Stamps audit timestamps from the injected clock and keeps the audit chain append-only.</summary>
    private void ApplyPersistenceRules()
    {
        var now = timeProvider.GetUtcNow();
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is AuditEntry && (entry.State is EntityState.Modified or EntityState.Deleted))
            {
                throw new InvalidOperationException("Audit entries are append-only and cannot be changed or deleted.");
            }

            if (entry.Entity is not AuditableEntity)
            {
                continue;
            }

            if (entry.State == EntityState.Added)
            {
                entry.Property(nameof(AuditableEntity.CreatedAt)).CurrentValue = now;
                entry.Property(nameof(AuditableEntity.UpdatedAt)).CurrentValue = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(AuditableEntity.UpdatedAt)).CurrentValue = now;
            }
        }
    }
}
