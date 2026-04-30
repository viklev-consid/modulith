using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Notifications.Integration.Subscribers;

/// <summary>
/// Provides an atomic, lease-scoped send-claim over a <see cref="NotificationLog"/> row to
/// prevent duplicate email delivery across handler retries and concurrent deliveries.
/// <para>
/// Protocol: (1) handler inserts log as <see cref="NotificationDeliveryStatus.Pending"/>;
/// (2) handler calls <see cref="TryClaimAsync"/> which atomically transitions
/// <c>Pending → Sending</c> and returns an opaque lease token; (3) handler sends the email
/// inside a try/catch that calls <see cref="MarkReadyAsync"/> (with the token) for
/// <c>RetryableSmtpException</c> or <see cref="MarkFailedAsync"/> (with the token) for
/// <c>TerminalSmtpException</c> before rethrowing; (4) handler calls
/// <see cref="MarkSentAsync"/> (with the token).
/// </para>
/// <para>
/// Every state-transition method requires the lease token returned by
/// <see cref="TryClaimAsync"/>. A transition is a no-op if the token does not match the
/// value stored on the row, which means a late or stale caller can never accidentally
/// advance a row it no longer owns.
/// </para>
/// <para>
/// Transient recovery: <see cref="MarkReadyAsync"/> resets <c>Sending → Pending</c>
/// immediately (and clears the token) so the Wolverine retry can re-claim without waiting
/// for the stale-row threshold.
/// </para>
/// <para>
/// Terminal failure: <see cref="MarkFailedAsync"/> transitions <c>Sending → Failed</c>;
/// the Wolverine message moves to the dead-letter queue.
/// </para>
/// <para>
/// Crash recovery: if the process terminates between steps 2 and 4 the row stays
/// <c>Sending</c>. <see cref="TryClaimAsync"/> detects rows whose
/// <see cref="NotificationLog.SendingClaimedAt"/> exceeds <see cref="StuckSendingThreshold"/>,
/// resets them to <c>Pending</c> (clearing the stale token), and re-claims with a fresh
/// token. This path is crash-recovery only — a row whose original sender is still legitimately
/// running will not be eligible because its <c>SendingClaimedAt</c> is recent, and even if
/// age-based reclaim were triggered the original sender's token would no longer match the
/// newly-issued one, making its subsequent <see cref="MarkSentAsync"/> a safe no-op.
/// </para>
/// </summary>
public sealed class NotificationSendGuard(NotificationsDbContext db, IClock clock)
{
    /// <summary>Rows stuck in Sending for longer than this are eligible for automatic recovery.</summary>
    private static readonly TimeSpan StuckSendingThreshold = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Attempts to atomically claim the send slot for <paramref name="idempotencyKey"/>.
    /// </summary>
    /// <returns>
    /// The opaque lease token if the caller holds the exclusive claim and should proceed to
    /// send the email; <c>null</c> if another attempt is in-flight or the notification was
    /// already sent or failed. Pass the non-null token to every subsequent guard call.
    /// </returns>
    public async Task<Guid?> TryClaimAsync(Guid idempotencyKey, CancellationToken ct)
    {
        // Fast path: row is Pending — claim it.
        var leaseToken = await AtomicClaimAsync(idempotencyKey, ct);
        if (leaseToken is not null)
        {
            return leaseToken;
        }

        // Row is Sending or Sent/Failed. Try to recover if it is a stale Sending row.
        var staleThreshold = clock.UtcNow - StuckSendingThreshold;
        var recovered = await db.NotificationLogs
            .Where(l => l.IdempotencyKey == idempotencyKey
                        && l.DeliveryStatus == NotificationDeliveryStatus.Sending
                        && l.SendingClaimedAt < staleThreshold)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.DeliveryStatus, NotificationDeliveryStatus.Pending)
                .SetProperty(l => l.SendingClaimedAt, (DateTimeOffset?)null)
                .SetProperty(l => l.SendingLeaseToken, (Guid?)null), ct);

        if (recovered == 0)
        {
            return null; // Not stale (still in-flight) or already Sent/Failed.
        }

        // The row is now Pending again. Re-claim with a fresh token; another concurrent
        // instance may have beaten us to it, in which case we return null and back off.
        return await AtomicClaimAsync(idempotencyKey, ct);
    }

    /// <summary>
    /// Resets a <see cref="NotificationDeliveryStatus.Sending"/> row back to
    /// <see cref="NotificationDeliveryStatus.Pending"/> so the next Wolverine retry can
    /// re-claim immediately. The transition only applies when <paramref name="leaseToken"/>
    /// matches the token stored on the row — a stale or duplicate caller is a no-op.
    /// <para>
    /// Call this in a <c>catch (RetryableSmtpException)</c> block before rethrowing.
    /// </para>
    /// </summary>
    public Task MarkReadyAsync(Guid idempotencyKey, Guid leaseToken, CancellationToken ct) =>
        db.NotificationLogs
            .Where(l => l.IdempotencyKey == idempotencyKey
                        && l.DeliveryStatus == NotificationDeliveryStatus.Sending
                        && l.SendingLeaseToken == leaseToken)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.DeliveryStatus, NotificationDeliveryStatus.Pending)
                .SetProperty(l => l.SendingClaimedAt, (DateTimeOffset?)null)
                .SetProperty(l => l.SendingLeaseToken, (Guid?)null), ct);

    /// <summary>
    /// Transitions <c>Sending → <see cref="NotificationDeliveryStatus.Failed"/></c>.
    /// Only applies when <paramref name="leaseToken"/> matches the stored token.
    /// Call this in a <c>catch (TerminalSmtpException)</c> block before rethrowing.
    /// </summary>
    public Task MarkFailedAsync(Guid idempotencyKey, Guid leaseToken, CancellationToken ct) =>
        db.NotificationLogs
            .Where(l => l.IdempotencyKey == idempotencyKey
                        && l.DeliveryStatus == NotificationDeliveryStatus.Sending
                        && l.SendingLeaseToken == leaseToken)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.DeliveryStatus, NotificationDeliveryStatus.Failed)
                .SetProperty(l => l.SendingClaimedAt, (DateTimeOffset?)null)
                .SetProperty(l => l.SendingLeaseToken, (Guid?)null), ct);

    /// <summary>
    /// Transitions <c>Sending → <see cref="NotificationDeliveryStatus.Sent"/></c>.
    /// Only applies when <paramref name="leaseToken"/> matches the stored token, so a
    /// late sender whose claim was superseded by crash-recovery cannot mark the row Sent.
    /// </summary>
    public Task MarkSentAsync(Guid idempotencyKey, Guid leaseToken, CancellationToken ct) =>
        db.NotificationLogs
            .Where(l => l.IdempotencyKey == idempotencyKey
                        && l.DeliveryStatus == NotificationDeliveryStatus.Sending
                        && l.SendingLeaseToken == leaseToken)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.DeliveryStatus, NotificationDeliveryStatus.Sent)
                .SetProperty(l => l.SentAt, clock.UtcNow)
                .SetProperty(l => l.SendingLeaseToken, (Guid?)null), ct);

    private async Task<Guid?> AtomicClaimAsync(Guid idempotencyKey, CancellationToken ct)
    {
        var leaseToken = Guid.NewGuid();
        var claimed = await db.NotificationLogs
            .Where(l => l.IdempotencyKey == idempotencyKey
                        && l.DeliveryStatus == NotificationDeliveryStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.DeliveryStatus, NotificationDeliveryStatus.Sending)
                .SetProperty(l => l.SendingClaimedAt, clock.UtcNow)
                .SetProperty(l => l.SendingLeaseToken, (Guid?)leaseToken), ct);
        return claimed > 0 ? leaseToken : null;
    }
}
