using Modulith.Modules.Notifications.Contracts.Dtos;

namespace Modulith.Modules.Notifications.Features.ListMyNotifications;

public sealed record ListMyNotificationsResponse(
    IReadOnlyList<MyNotificationResponse> Items,
    DateTimeOffset? NextBefore,
    Guid? NextBeforeId);

public sealed record MyNotificationResponse(
    Guid Id,
    string Type,
    NotificationCategory Category,
    NotificationSeverity Severity,
    string Title,
    string Body,
    NotificationLinkDto? Link,
    bool IsRead,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);
