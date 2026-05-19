using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Jobs;

/// <summary>Scheduled daily to delete expired tokens beyond the grace period.</summary>
public sealed record SweepExpiredTokens;

public sealed class SweepExpiredTokensHandler(UsersDbContext db, IClock clock)
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

        await db.PendingTwoFactorChallenges
            .Where(p => p.ExpiresAt < clock.UtcNow || p.ConsumedAt != null)
            .ExecuteDeleteAsync(ct);
    }
}
