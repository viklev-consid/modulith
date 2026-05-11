namespace Modulith.Modules.Notifications.Features.MarkNotificationAsRead;

public sealed record MarkNotificationAsReadCommand(Guid UserId, Guid NotificationId);
