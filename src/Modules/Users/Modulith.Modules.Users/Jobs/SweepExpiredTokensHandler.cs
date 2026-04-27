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

        // Pending email changes have no expiry of their own; they become stale once the
        // associated SingleUseToken has been swept. Delete any that no longer have a token.
        await db.PendingEmailChanges
            .Where(p => !db.SingleUseTokens.Any(t => t.Id == p.TokenId))
            .ExecuteDeleteAsync(ct);

        // Pending external logins: delete expired and consumed records immediately (no grace period).
        // Consumed records are safe to delete — they are single-use and the confirmation is complete.
        await db.PendingExternalLogins
            .Where(p => p.ExpiresAt < clock.UtcNow || p.ConsumedAt != null)
            .ExecuteDeleteAsync(ct);

        // Re-schedule for next day.
        await bus.PublishAsync(new SweepExpiredTokens(), new DeliveryOptions { ScheduledTime = clock.UtcNow.AddDays(1) });
    }
}
