using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modulith.Modules.Notifications.Domain;

namespace Modulith.Modules.Notifications.Persistence.Configurations;

internal sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, v => new NotificationPreferenceId(v));

        builder.Property(p => p.UserId)
            .IsRequired();

        builder.Property(p => p.Category)
            .IsRequired();

        builder.Property(p => p.BellEnabled)
            .IsRequired();

        builder.Property(p => p.EmailEnabled)
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .IsRequired();

        builder.HasIndex(p => new { p.UserId, p.Category })
            .IsUnique();
    }
}
