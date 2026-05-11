using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Notifications.Jobs;

public sealed record PruneNotifications;

public sealed class PruneNotificationsHandler(
    NotificationsDbContext db,
    IClock clock)
{
    public async Task Handle(PruneNotifications _, CancellationToken ct)
    {
        var now = clock.UtcNow;

        await db.UserNotifications
            .Where(n => n.RetentionUntil < now)
            .ExecuteDeleteAsync(ct);
    }
}
