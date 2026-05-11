using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Notifications.Streaming;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Notifications.Features.MarkAllNotificationsAsRead;

public sealed class MarkAllNotificationsAsReadHandler(
    NotificationsDbContext db,
    IClock clock,
    IOptions<NotificationsOptions> options,
    INotificationStreamPublisher streamPublisher)
{
    public async Task<ErrorOr<Success>> Handle(MarkAllNotificationsAsReadCommand command, CancellationToken ct)
    {
        var readAt = clock.UtcNow;
        var defaultRetention = readAt.AddDays(options.Value.Retention.DefaultReadDays);
        var protectedRetention = readAt.AddDays(options.Value.Retention.SecurityAndAccountDays);

        await db.UserNotifications
            .Where(n => n.RecipientUserId == command.UserId && n.ReadAt == null && n.ArchivedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.ReadAt, readAt)
                .SetProperty(
                    n => n.RetentionUntil,
                    n => n.Category == BellNotificationCategory.Account || n.Category == BellNotificationCategory.Security
                        ? protectedRetention
                        : defaultRetention), ct);

        await streamPublisher.PublishAsync(
            command.UserId,
            new NotificationStreamEvent("unread-count.changed", """{"count":0}"""),
            ct);

        return Result.Success;
    }
}
