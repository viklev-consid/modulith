using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Notifications.Errors;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Notifications.Policies;
using Modulith.Modules.Notifications.Streaming;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Notifications.Features.MarkNotificationAsRead;

public sealed class MarkNotificationAsReadHandler(
    NotificationsDbContext db,
    IClock clock,
    NotificationRetentionPolicy retentionPolicy,
    INotificationStreamPublisher streamPublisher)
{
    public async Task<ErrorOr<Success>> Handle(MarkNotificationAsReadCommand command, CancellationToken ct)
    {
        var notification = await db.UserNotifications
            .SingleOrDefaultAsync(n => n.Id == new UserNotificationId(command.NotificationId)
                                       && n.RecipientUserId == command.UserId
                                       && n.ArchivedAt == null, ct);

        if (notification is null)
        {
            return NotificationsErrors.NotificationNotFound;
        }

        var readAt = clock.UtcNow;
        notification.MarkRead(readAt, retentionPolicy.GetReadRetentionUntil(notification.Category, readAt));
        await db.SaveChangesAsync(ct);

        await streamPublisher.PublishAsync(
            command.UserId,
            new NotificationStreamEvent("notification.read", $$"""
            {"id":"{{command.NotificationId}}","unreadCountChanged":true}
            """),
            ct);

        return Result.Success;
    }
}
