using Modulith.Modules.Notifications.Domain;

namespace Modulith.Modules.Notifications.Policies;

internal sealed record NotificationPreferenceDefault(
    BellNotificationCategory Category,
    bool BellEnabled,
    bool EmailEnabled,
    bool IsLocked);

internal static class NotificationPreferenceDefaults
{
    public static IReadOnlyList<NotificationPreferenceDefault> All { get; } =
    [
        new(BellNotificationCategory.Product, BellEnabled: true, EmailEnabled: false, IsLocked: false),
        new(BellNotificationCategory.Collaboration, BellEnabled: true, EmailEnabled: false, IsLocked: false),
        new(BellNotificationCategory.System, BellEnabled: true, EmailEnabled: false, IsLocked: false),
        new(BellNotificationCategory.Account, BellEnabled: false, EmailEnabled: true, IsLocked: true),
        new(BellNotificationCategory.Security, BellEnabled: false, EmailEnabled: true, IsLocked: true),
    ];

    public static NotificationPreferenceDefault Get(BellNotificationCategory category) =>
        All.Single(p => p.Category == category);
}
