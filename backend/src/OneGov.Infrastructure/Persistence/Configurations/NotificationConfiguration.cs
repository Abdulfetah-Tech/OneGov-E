using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneGov.Domain.Notifications;
using OneGov.Infrastructure.Identity;

namespace OneGov.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.Property(n => n.ReferenceNumber).HasMaxLength(48);
        builder.Property(n => n.Transition).HasMaxLength(64).IsRequired();
        builder.Property(n => n.ToState).HasMaxLength(40).IsRequired();
        builder.Property(n => n.Note).HasMaxLength(2000);
        builder.Ignore(n => n.IsRead);

        // The bell asks "how many unread does this person have?" on every page, so that lookup is indexed.
        builder.HasIndex(n => new { n.UserId, n.ReadAt });
        builder.HasIndex(n => new { n.UserId, n.CreatedAt });
        builder.HasIndex(n => n.ApplicationId);

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
