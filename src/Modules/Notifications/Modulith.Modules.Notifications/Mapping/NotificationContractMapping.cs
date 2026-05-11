using Modulith.Modules.Notifications.Contracts.Dtos;
using Modulith.Modules.Notifications.Domain;

namespace Modulith.Modules.Notifications.Mapping;

internal static class NotificationContractMapping
{
    public static BellNotificationCategory ToDomain(this NotificationCategory category) =>
        category switch
        {
            NotificationCategory.Product => BellNotificationCategory.Product,
            NotificationCategory.Collaboration => BellNotificationCategory.Collaboration,
            NotificationCategory.System => BellNotificationCategory.System,
            NotificationCategory.Account => BellNotificationCategory.Account,
            NotificationCategory.Security => BellNotificationCategory.Security,
            _ => BellNotificationCategory.System,
        };

    public static BellNotificationSeverity ToDomain(this NotificationSeverity severity) =>
        severity switch
        {
            NotificationSeverity.Info => BellNotificationSeverity.Info,
            NotificationSeverity.Success => BellNotificationSeverity.Success,
            NotificationSeverity.Warning => BellNotificationSeverity.Warning,
            NotificationSeverity.Critical => BellNotificationSeverity.Critical,
            _ => BellNotificationSeverity.Info,
        };

    public static NotificationCategory ToContract(this BellNotificationCategory category) =>
        category switch
        {
            BellNotificationCategory.Product => NotificationCategory.Product,
            BellNotificationCategory.Collaboration => NotificationCategory.Collaboration,
            BellNotificationCategory.System => NotificationCategory.System,
            BellNotificationCategory.Account => NotificationCategory.Account,
            BellNotificationCategory.Security => NotificationCategory.Security,
            _ => NotificationCategory.System,
        };

    public static NotificationSeverity ToContract(this BellNotificationSeverity severity) =>
        severity switch
        {
            BellNotificationSeverity.Info => NotificationSeverity.Info,
            BellNotificationSeverity.Success => NotificationSeverity.Success,
            BellNotificationSeverity.Warning => NotificationSeverity.Warning,
            BellNotificationSeverity.Critical => NotificationSeverity.Critical,
            _ => NotificationSeverity.Info,
        };
}
