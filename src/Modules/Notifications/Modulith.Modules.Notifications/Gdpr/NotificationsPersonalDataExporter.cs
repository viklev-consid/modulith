using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Shared.Kernel.Gdpr;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Notifications.Gdpr;

public sealed class NotificationsPersonalDataExporter(NotificationsDbContext db) : IPersonalDataExporter
{
    public async Task<PersonalDataExport> ExportAsync(UserRef user, CancellationToken ct)
    {
        var logs = await db.NotificationLogs
            .Where(l => l.UserId == user.UserId)
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

        return new PersonalDataExport(user.UserId, "Notifications", data);
    }
}
