using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Jobs;

/// <summary>Scheduled daily to delete expired tokens beyond the grace period.</summary>
public sealed record SweepExpiredTokens;

public sealed class SweepExpiredTokensHandler(UsersDbContext db, IClock clock, IMessageBus bus)
{
    public async Task Handle(SweepExpiredTokens _, CancellationToken ct)
    {
        // Retain tokens for 7 days past expiry for audit/forensics.
        var cutoff = clock.UtcNow.AddDays(-7);

        await db.RefreshTokens
            .Where(t => t.ExpiresAt < cutoff)
            .ExecuteDeleteAsync(ct);

        await db.SingleUseTokens
            .Where(t => t.ExpiresAt < cutoff)
            .ExecuteDeleteAsync(ct);

        // Re-schedule for next day.
        await bus.PublishAsync(new SweepExpiredTokens(), new DeliveryOptions { ScheduledTime = clock.UtcNow.AddDays(1) });
    }
}
