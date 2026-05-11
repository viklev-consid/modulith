using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Shared.Kernel.Gdpr;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Notifications.Gdpr;

public sealed class NotificationsPersonalDataExporter(NotificationsDbContext db) : IPersonalDataExporter
{
    public async Task<PersonalDataExport> ExportAsync(UserRef user, CancellationToken ct)
    {
        var logs = await db.NotificationLogs
            .Where(l => l.UserId == user.UserId
                        && l.DeliveryStatus == NotificationDeliveryStatus.Sent)
            .OrderBy(l => l.SentAt)
            .Select(l => new { l.NotificationType, l.Subject, l.SentAt })
            .ToListAsync(ct);

        var data = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["notificationsSent"] = logs.Select(l => new
            {
                type = l.NotificationType.ToString(),
                subject = l.Subject,
                sentAt = l.SentAt,
            }).ToList(),
        };

        data["inAppNotifications"] = await db.UserNotifications
            .Where(n => n.RecipientUserId == user.UserId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new
            {
                type = n.Type,
                category = n.Category.ToString(),
                severity = n.Severity.ToString(),
                title = n.Title,
                body = n.Body,
                linkHref = n.LinkHref,
                createdAt = n.CreatedAt,
                readAt = n.ReadAt,
                archivedAt = n.ArchivedAt,
                expiresAt = n.ExpiresAt,
                retentionUntil = n.RetentionUntil,
            })
            .ToListAsync(ct);

        data["notificationPreferences"] = await db.NotificationPreferences
            .Where(p => p.UserId == user.UserId)
            .OrderBy(p => p.Category)
            .Select(p => new
            {
                category = p.Category.ToString(),
                bellEnabled = p.BellEnabled,
                emailEnabled = p.EmailEnabled,
                updatedAt = p.UpdatedAt,
            })
            .ToListAsync(ct);

        return new PersonalDataExport(user.UserId, "Notifications", data);
    }
}
