using Microsoft.Extensions.Options;
using Modulith.Modules.Notifications.Domain;

namespace Modulith.Modules.Notifications.Policies;

public sealed class NotificationRetentionPolicy(IOptions<NotificationsOptions> options)
{
    public DateTimeOffset GetRetentionUntil(
        BellNotificationCategory category,
        DateTimeOffset createdAt)
    {
        var days = category is BellNotificationCategory.Account or BellNotificationCategory.Security
            ? options.Value.Retention.SecurityAndAccountDays
            : options.Value.Retention.DefaultUnreadDays;

        return createdAt.AddDays(days);
    }

    public DateTimeOffset GetReadRetentionUntil(
        BellNotificationCategory category,
        DateTimeOffset readAt)
    {
        var days = category is BellNotificationCategory.Account or BellNotificationCategory.Security
            ? options.Value.Retention.SecurityAndAccountDays
            : options.Value.Retention.DefaultReadDays;

        return readAt.AddDays(days);
    }

    public DateTimeOffset GetArchivedRetentionUntil(
        BellNotificationCategory category,
        DateTimeOffset archivedAt)
    {
        var days = category is BellNotificationCategory.Account or BellNotificationCategory.Security
            ? options.Value.Retention.SecurityAndAccountDays
            : options.Value.Retention.DefaultArchivedDays;

        return archivedAt.AddDays(days);
    }
}
