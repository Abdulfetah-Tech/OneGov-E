using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneGov.Domain.Applications;
using OneGov.Domain.Registry;

namespace OneGov.Infrastructure.Persistence.Configurations;

internal sealed class ServiceApplicationConfiguration : IEntityTypeConfiguration<ServiceApplication>
{
    public void Configure(EntityTypeBuilder<ServiceApplication> builder)
    {
        builder.ToTable("applications");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.WorkflowCode).HasMaxLength(64).IsRequired();
        builder.Property(a => a.State).HasMaxLength(40).IsRequired();
        builder.Property(a => a.ReferenceNumber).HasMaxLength(48);
        builder.HasIndex(a => a.ReferenceNumber).IsUnique().HasFilter("reference_number IS NOT NULL");
        builder.Property(a => a.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(a => new { a.AgencyId, a.State, a.CreatedAt });
        builder.HasIndex(a => new { a.CitizenId, a.CreatedAt });

        // Optimistic concurrency via PostgreSQL's xmin system column.
        builder.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();

        // CitizenId gets a foreign key when the citizens module is added (Phase 3).
        builder.HasOne<GovService>().WithMany().HasForeignKey(a => a.ServiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Agency>().WithMany().HasForeignKey(a => a.AgencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(a => a.History).WithOne().HasForeignKey(h => h.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.History).UsePropertyAccessMode(PropertyAccessMode.Field);

        // The query filter (soft delete plus who may see the row) lives in OneGovDbContext, because it depends on the caller.
    }
}

internal sealed class ApplicationHistoryEntryConfiguration : IEntityTypeConfiguration<ApplicationHistoryEntry>
{
    public void Configure(EntityTypeBuilder<ApplicationHistoryEntry> builder)
    {
        builder.ToTable("application_history");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();
        builder.Property(h => h.Transition).HasMaxLength(64).IsRequired();
        builder.Property(h => h.FromState).HasMaxLength(40).IsRequired();
        builder.Property(h => h.ToState).HasMaxLength(40).IsRequired();
        builder.Property(h => h.Note).HasMaxLength(2000);
        builder.HasIndex(h => new { h.ApplicationId, h.OccurredAt });
    }
}
