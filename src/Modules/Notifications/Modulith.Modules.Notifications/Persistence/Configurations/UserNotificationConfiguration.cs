using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modulith.Modules.Notifications.Domain;

namespace Modulith.Modules.Notifications.Persistence.Configurations;

internal sealed class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id)
            .HasConversion(id => id.Value, v => new UserNotificationId(v));

        builder.Property(n => n.RecipientUserId)
            .IsRequired();

        builder.Property(n => n.Type)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(n => n.Category)
            .IsRequired();

        builder.Property(n => n.Severity)
            .IsRequired();

        builder.Property(n => n.Title)
            .HasMaxLength(240)
            .IsRequired();

        builder.Property(n => n.Body)
            .HasMaxLength(2_000)
            .IsRequired();

        builder.Property(n => n.LinkHref)
            .HasMaxLength(1_000)
            .IsRequired(false);

        builder.Property(n => n.LinkLabel)
            .HasMaxLength(120)
            .IsRequired(false);

        builder.Property(n => n.CreatedAt)
            .IsRequired();

        builder.Property(n => n.ReadAt)
            .IsRequired(false);

        builder.Property(n => n.ArchivedAt)
            .IsRequired(false);

        builder.Property(n => n.ExpiresAt)
            .IsRequired(false);

        builder.Property(n => n.RetentionUntil)
            .IsRequired();

        builder.Property(n => n.IdempotencyKey)
            .IsRequired();

        builder.HasIndex(n => new { n.RecipientUserId, n.CreatedAt });
        builder.HasIndex(n => new { n.RecipientUserId, n.ReadAt, n.CreatedAt });
        builder.HasIndex(n => new { n.RecipientUserId, n.ArchivedAt, n.CreatedAt });
        builder.HasIndex(n => n.RetentionUntil);
        builder.HasIndex(n => n.ExpiresAt);
        builder.HasIndex(n => new { n.RecipientUserId, n.IdempotencyKey }).IsUnique();
    }
}
