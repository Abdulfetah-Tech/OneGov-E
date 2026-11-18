using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneGov.Domain.Registry;

namespace OneGov.Infrastructure.Persistence.Configurations;

internal static class LocalizedTextMapping
{
    /// <summary>Maps a <see cref="LocalizedText"/> to three columns: {prefix}_en, {prefix}_am, {prefix}_om.</summary>
    public static EntityTypeBuilder<T> OwnsLocalized<T>(
        this EntityTypeBuilder<T> builder,
        Expression<Func<T, LocalizedText?>> navigation,
        string prefix,
        int maxLength)
        where T : class
    {
        builder.OwnsOne(navigation, owned =>
        {
            owned.Property(text => text.En).HasColumnName($"{prefix}_en").HasMaxLength(maxLength).IsRequired();
            owned.Property(text => text.Am).HasColumnName($"{prefix}_am").HasMaxLength(maxLength).IsRequired();
            owned.Property(text => text.Om).HasColumnName($"{prefix}_om").HasMaxLength(maxLength).IsRequired();
        });
        builder.Navigation(navigation).IsRequired();
        return builder;
    }
}
