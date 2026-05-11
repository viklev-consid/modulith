namespace Modulith.Modules.Notifications;

internal static class NotificationsRoutes
{
    public const string MyNotifications = "/v1/me/notifications";
    public const string MyNotificationById = "/v1/me/notifications/{notificationId:guid}";
    public const string MyNotificationRead = "/v1/me/notifications/{notificationId:guid}/read";
    public const string MyNotificationsReadAll = "/v1/me/notifications/read-all";
    public const string MyNotificationsUnreadCount = "/v1/me/notifications/unread-count";
    public const string MyNotificationsStream = "/v1/me/notifications/stream";
    public const string MyNotificationPreferences = "/v1/me/notification-preferences";
}
