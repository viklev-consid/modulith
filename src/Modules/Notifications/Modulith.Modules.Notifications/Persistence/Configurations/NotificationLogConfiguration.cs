using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modulith.Modules.Notifications.Domain;

namespace Modulith.Modules.Notifications.Persistence.Configurations;

internal sealed class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id)
            .HasConversion(id => id.Value, v => new NotificationLogId(v));

        builder.Property(n => n.UserId)
            .IsRequired();

        builder.Property(n => n.RecipientEmail)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(n => n.NotificationType)
            .IsRequired();

        builder.Property(n => n.Subject)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(n => n.SentAt)
            .IsRequired();

        builder.HasIndex(n => n.UserId);
        builder.HasIndex(n => new { n.UserId, n.NotificationType });
    }
}
