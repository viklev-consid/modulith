using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Notifications.Contracts.Commands;
using Modulith.Modules.Notifications.Contracts.Dtos;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Notifications.Errors;
using Modulith.Modules.Notifications.Mapping;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Notifications.Policies;
using Modulith.Modules.Notifications.Streaming;
using Modulith.Shared.Infrastructure.Persistence;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Notifications.Features.CreateNotification;

public sealed class CreateNotificationHandler(
    NotificationsDbContext db,
    IClock clock,
    NotificationRetentionPolicy retentionPolicy,
    INotificationStreamPublisher streamPublisher)
{
    public async Task<ErrorOr<CreateNotificationResponse>> Handle(CreateNotificationCommand command, CancellationToken ct)
    {
        if (command.RecipientUserId == Guid.Empty)
        {
            return NotificationsErrors.RecipientInvalid;
        }

        if (string.IsNullOrWhiteSpace(command.Type))
        {
            return NotificationsErrors.NotificationTypeRequired;
        }

        if (string.IsNullOrWhiteSpace(command.Title))
        {
            return NotificationsErrors.NotificationTitleRequired;
        }

        if (command.Body is null
            || command.Link is { Href: null }
            || command.Type.Length > 200
            || command.Title.Length > 240
            || command.Body.Length > 2_000
            || command.Link?.Href.Length > 1_000
            || command.Link?.Label?.Length > 120
            || command.IdempotencyKey == Guid.Empty
            || command.OccurredAt == default
            || command.OccurredAt > clock.UtcNow
            || !Enum.IsDefined(command.Category)
            || !Enum.IsDefined(command.Severity)
            || command.Channels?.Any(channel => !Enum.IsDefined(channel)) == true)
        {
            return NotificationsErrors.NotificationInvalid;
        }

        var category = command.Category.ToDomain();
        var requestedChannels = command.Channels ?? GetDefaultChannels(category);

        if (!requestedChannels.Contains(NotificationChannel.Bell))
        {
            return new CreateNotificationResponse(null);
        }

        var preference = await db.NotificationPreferences
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.UserId == command.RecipientUserId && p.Category == category, ct);

        var defaults = NotificationPreferenceDefaults.Get(category);
        var bellEnabled = preference?.BellEnabled ?? defaults.BellEnabled;

        if (!bellEnabled)
        {
            return new CreateNotificationResponse(null);
        }

        var createdAt = command.OccurredAt;
        var notification = UserNotification.Create(
            command.RecipientUserId,
            command.Type.Trim(),
            category,
            command.Severity.ToDomain(),
            command.Title.Trim(),
            command.Body.Trim(),
            command.Link?.Href,
            command.Link?.Label,
            createdAt,
            expiresAt: null,
            retentionPolicy.GetRetentionUntil(category, createdAt),
            command.IdempotencyKey);

        db.UserNotifications.Add(notification);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            db.Entry(notification).State = EntityState.Detached;

            var existingId = await db.UserNotifications
                .AsNoTracking()
                .Where(n => n.RecipientUserId == command.RecipientUserId && n.IdempotencyKey == command.IdempotencyKey)
                .Select(n => (Guid?)n.Id.Value)
                .SingleOrDefaultAsync(ct);

            return new CreateNotificationResponse(existingId);
        }

        await streamPublisher.PublishAsync(
            command.RecipientUserId,
            new NotificationStreamEvent("notification.created", $$"""
            {"id":"{{notification.Id.Value}}","unreadCountChanged":true}
            """),
            ct);

        return new CreateNotificationResponse(notification.Id.Value);
    }

    private static HashSet<NotificationChannel> GetDefaultChannels(BellNotificationCategory category) =>
        category switch
        {
            BellNotificationCategory.Account or BellNotificationCategory.Security => new HashSet<NotificationChannel> { NotificationChannel.Email },
            _ => new HashSet<NotificationChannel> { NotificationChannel.Bell },
        };
}
