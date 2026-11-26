using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneGov.Domain.Documents;

namespace OneGov.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationDocumentConfiguration : IEntityTypeConfiguration<ApplicationDocument>
{
    public void Configure(EntityTypeBuilder<ApplicationDocument> builder)
    {
        builder.ToTable("application_documents");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.FileName).HasMaxLength(120).IsRequired();
        builder.Property(d => d.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(d => d.Sha256).HasMaxLength(64).IsRequired();
        builder.Property(d => d.StorageKey).HasMaxLength(32).IsRequired();
        builder.Property(d => d.ScanStatus).HasMaxLength(24).IsRequired();

        // Deleted documents stay in the table for the record but are invisible to every query.
        builder.HasQueryFilter(d => d.DeletedAt == null);

        builder.HasIndex(d => new { d.ApplicationId, d.CreatedAt });
        builder.HasIndex(d => d.StorageKey).IsUnique();
    }
}
