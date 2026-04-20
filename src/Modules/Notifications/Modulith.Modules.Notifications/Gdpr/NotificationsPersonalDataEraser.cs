using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Shared.Kernel.Gdpr;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Notifications.Gdpr;

public sealed class NotificationsPersonalDataEraser(NotificationsDbContext db) : IPersonalDataEraser
{
    public async Task<ErasureResult> EraseAsync(UserRef user, ErasureStrategy strategy, CancellationToken ct)
    {
        var logs = await db.NotificationLogs
            .Where(l => l.UserId == user.UserId)
            .ToListAsync(ct);

        db.NotificationLogs.RemoveRange(logs);
        await db.SaveChangesAsync(ct);

        return new ErasureResult(user.UserId, ErasureStrategy.HardDelete, logs.Count);
    }
}
