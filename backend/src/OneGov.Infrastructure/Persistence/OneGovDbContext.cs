using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OneGov.Application.Identity;
using OneGov.Domain.Applications;
using OneGov.Domain.Audit;
using OneGov.Domain.Common;
using OneGov.Domain.Identity;
using OneGov.Domain.Registry;
using OneGov.Infrastructure.Identity;

namespace OneGov.Infrastructure.Persistence;

/// <param name="dataScope">Who is asking. When missing (design time, tests that do not care) applications are not visible.</param>
public sealed class OneGovDbContext(
    DbContextOptions<OneGovDbContext> options, TimeProvider timeProvider, IDataScopeAccessor? dataScope = null)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<Agency> Agencies => Set<Agency>();

    public DbSet<GovService> Services => Set<GovService>();

    public DbSet<ServiceApplication> Applications => Set<ServiceApplication>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity's own mapping first, then ours, so our table names win.
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasSequence<long>("application_reference_seq");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OneGovDbContext).Assembly);

        // Isolation in the data layer: a caller only ever sees their own applications and, for staff, their own agency's.
        // This must stay in the context so EF can read the current caller per query. Fails closed when no scope is known.
        modelBuilder.Entity<ServiceApplication>().HasQueryFilter(a =>
            !a.IsDeleted
            && (CurrentScope.IsUnrestricted
                || a.CitizenId == CurrentScope.CitizenId
                || a.AgencyId == CurrentScope.AgencyId));
    }

    private DataScope CurrentScope => dataScope?.Current ?? DataScope.None;

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
