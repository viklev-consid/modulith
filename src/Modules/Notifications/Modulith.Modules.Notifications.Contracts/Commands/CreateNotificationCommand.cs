using Modulith.Modules.Notifications.Contracts.Dtos;

namespace Modulith.Modules.Notifications.Contracts.Commands;

public sealed record CreateNotificationCommand(
    Guid RecipientUserId,
    string Type,
    NotificationCategory Category,
    NotificationSeverity Severity,
    string Title,
    string Body,
    NotificationLinkDto? Link,
    IReadOnlySet<NotificationChannel>? Channels,
    Guid IdempotencyKey,
    DateTimeOffset OccurredAt);

public sealed record CreateNotificationResponse(Guid? BellNotificationId);
