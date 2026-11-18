using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneGov.Domain.Registry;

namespace OneGov.Infrastructure.Persistence.Configurations;

internal sealed class AgencyConfiguration : IEntityTypeConfiguration<Agency>
{
    public void Configure(EntityTypeBuilder<Agency> builder)
    {
        builder.ToTable("agencies");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Code).HasMaxLength(48).IsRequired();
        builder.HasIndex(a => a.Code).IsUnique().HasFilter("is_deleted = false");
        builder.OwnsLocalized(a => a.Name, "name", 200);
        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}

internal sealed class GovServiceConfiguration : IEntityTypeConfiguration<GovService>
{
    public void Configure(EntityTypeBuilder<GovService> builder)
    {
        builder.ToTable("gov_services");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Code).HasMaxLength(64).IsRequired();
        builder.HasIndex(s => s.Code).IsUnique().HasFilter("is_deleted = false");
        builder.Property(s => s.Category).HasConversion<string>().HasMaxLength(48).IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(s => s.WorkflowCode).HasMaxLength(64);
        builder.HasIndex(s => new { s.Category, s.Status });
        builder.OwnsLocalized(s => s.Name, "name", 200);
        builder.OwnsLocalized(s => s.Description, "description", 2000);

        builder.HasOne(s => s.Agency).WithMany().HasForeignKey(s => s.AgencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(s => s.Requirements).WithOne().HasForeignKey(r => r.ServiceId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}

internal sealed class ServiceRequirementConfiguration : IEntityTypeConfiguration<ServiceRequirement>
{
    public void Configure(EntityTypeBuilder<ServiceRequirement> builder)
    {
        builder.ToTable("service_requirements");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Kind).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.HasIndex(r => new { r.ServiceId, r.Order });
        builder.OwnsLocalized(r => r.Text, "text", 500);
    }
}
