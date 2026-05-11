using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Notifications.Errors;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Notifications.Policies;
using Modulith.Modules.Notifications.Streaming;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Notifications.Features.ArchiveNotification;

public sealed class ArchiveNotificationHandler(
    NotificationsDbContext db,
    IClock clock,
    NotificationRetentionPolicy retentionPolicy,
    INotificationStreamPublisher streamPublisher)
{
    public async Task<ErrorOr<Success>> Handle(ArchiveNotificationCommand command, CancellationToken ct)
    {
        var notification = await db.UserNotifications
            .SingleOrDefaultAsync(n => n.Id == new UserNotificationId(command.NotificationId)
                                       && n.RecipientUserId == command.UserId
                                       && n.ArchivedAt == null, ct);

        if (notification is null)
        {
            return NotificationsErrors.NotificationNotFound;
        }

        var archivedAt = clock.UtcNow;
        notification.Archive(archivedAt, retentionPolicy.GetArchivedRetentionUntil(notification.Category, archivedAt));
        await db.SaveChangesAsync(ct);

        await streamPublisher.PublishAsync(
            command.UserId,
            new NotificationStreamEvent("notification.archived", $$"""
            {"id":"{{command.NotificationId}}","unreadCountChanged":{{(!notification.IsRead).ToString().ToLowerInvariant()}}}
            """),
            ct);

        return Result.Success;
    }
}
