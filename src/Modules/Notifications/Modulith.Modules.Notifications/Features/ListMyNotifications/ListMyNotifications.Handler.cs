using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Notifications.Contracts.Dtos;
using Modulith.Modules.Notifications.Mapping;
using Modulith.Modules.Notifications.Persistence;

namespace Modulith.Modules.Notifications.Features.ListMyNotifications;

public sealed class ListMyNotificationsHandler(NotificationsDbContext db)
{
    public async Task<ErrorOr<ListMyNotificationsResponse>> Handle(ListMyNotificationsQuery query, CancellationToken ct)
    {
        var limit = Math.Clamp(query.Limit, 1, 100);
        var notifications = db.UserNotifications
            .AsNoTracking()
            .Where(n => n.RecipientUserId == query.UserId && n.ArchivedAt == null);

        notifications = query.Status?.Trim().ToLowerInvariant() switch
        {
            "unread" => notifications.Where(n => n.ReadAt == null),
            "read" => notifications.Where(n => n.ReadAt != null),
            _ => notifications,
        };

        if (query.Before is not null)
        {
            notifications = notifications.Where(n => n.CreatedAt < query.Before);
        }

        var items = await notifications
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit + 1)
            .Select(n => new
            {
                n.Id,
                n.Type,
                n.Category,
                n.Severity,
                n.Title,
                n.Body,
                n.LinkHref,
                n.LinkLabel,
                n.ReadAt,
                n.CreatedAt,
            })
            .ToListAsync(ct);

        var nextBefore = items.Count > limit ? items[^1].CreatedAt : (DateTimeOffset?)null;

        return new ListMyNotificationsResponse(
            items.Take(limit).Select(n => new MyNotificationResponse(
                n.Id.Value,
                n.Type,
                n.Category.ToContract(),
                n.Severity.ToContract(),
                n.Title,
                n.Body,
                n.LinkHref is null ? null : new NotificationLinkDto(n.LinkHref, n.LinkLabel),
                n.ReadAt is not null,
                n.CreatedAt,
                n.ReadAt)).ToList(),
            nextBefore);
    }
}
