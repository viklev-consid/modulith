namespace Modulith.Modules.Notifications.Features.ListMyNotifications;

public sealed record ListMyNotificationsQuery(
    Guid UserId,
    string? Status,
    int Limit,
    DateTimeOffset? Before);
