using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneGov.Domain.Audit;

namespace OneGov.Infrastructure.Persistence.Configurations;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entries");
        builder.HasKey(e => e.Sequence);
        builder.Property(e => e.Sequence).ValueGeneratedNever();
        builder.Property(e => e.Actor).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Action).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Purpose).HasMaxLength(500).IsRequired();
        builder.Property(e => e.PreviousHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(e => e.Hash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.HasIndex(e => e.Hash).IsUnique();
        builder.HasIndex(e => new { e.Actor, e.OccurredAtUnixMs });
        builder.Ignore(e => e.OccurredAt);
    }
}
