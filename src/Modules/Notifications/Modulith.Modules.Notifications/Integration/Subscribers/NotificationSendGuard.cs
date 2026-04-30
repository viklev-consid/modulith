using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Notifications.Integration.Subscribers;

/// <summary>
/// Provides an atomic send-claim over a <see cref="NotificationLog"/> row to prevent
/// duplicate email delivery across handler retries.
/// <para>
/// Protocol: (1) handler inserts log as <see cref="NotificationDeliveryStatus.Pending"/>;
/// (2) handler calls <see cref="TryClaimAsync"/> which atomically transitions
/// <c>Pending → Sending</c>; (3) handler sends the email inside a try/catch that calls
/// <see cref="MarkReadyAsync"/> for <c>RetryableSmtpException</c> or
/// <see cref="MarkFailedAsync"/> for <c>TerminalSmtpException</c> before rethrowing;
/// (4) handler calls <see cref="MarkSentAsync"/>.
/// </para>
/// <para>
/// Transient recovery: when <see cref="MarkReadyAsync"/> is called after a
/// <c>RetryableSmtpException</c>, the row is reset from <c>Sending → Pending</c>
/// immediately so the Wolverine retry can re-claim without waiting for the stale-row
/// threshold.
/// </para>
/// <para>
/// Terminal failure: when <see cref="MarkFailedAsync"/> is called after a
/// <c>TerminalSmtpException</c>, the row transitions <c>Sending → Failed</c> and the
/// Wolverine message moves to the dead-letter queue.
/// </para>
/// <para>
/// Crash recovery: if the process terminates between steps 2 and 4 the row stays <c>Sending</c>.
/// <see cref="TryClaimAsync"/> detects rows whose <see cref="NotificationLog.SendingClaimedAt"/>
/// exceeds <see cref="StuckSendingThreshold"/> and resets them to <c>Pending</c> before
/// re-claiming, so the next Wolverine retry can proceed.
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
    /// <c>true</c> if the caller holds the exclusive claim and should proceed to send the email;
    /// <c>false</c> if another attempt is in-flight or the notification was already sent.
    /// </returns>
    public async Task<bool> TryClaimAsync(Guid idempotencyKey, CancellationToken ct)
    {
        // Fast path: row is Pending — claim it.
        var claimed = await AtomicClaimAsync(idempotencyKey, ct);
        if (claimed > 0)
        {
            return true;
        }

        // Row is Sending or Sent. Try to recover if it is a stale Sending row.
        var staleThreshold = clock.UtcNow - StuckSendingThreshold;
        var recovered = await db.NotificationLogs
            .Where(l => l.IdempotencyKey == idempotencyKey
                        && l.DeliveryStatus == NotificationDeliveryStatus.Sending
                        && l.SendingClaimedAt < staleThreshold)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.DeliveryStatus, NotificationDeliveryStatus.Pending)
                .SetProperty(l => l.SendingClaimedAt, (DateTimeOffset?)null), ct);

        if (recovered == 0)
        {
            return false; // Not stale (still in-flight) or already Sent.
        }

        // The row is now Pending again. Re-claim; another concurrent instance may have
        // beaten us to it, in which case claimed == 0 and we back off.
        claimed = await AtomicClaimAsync(idempotencyKey, ct);
        return claimed > 0;
    }

    /// <summary>
    /// Releases the send claim by resetting a <see cref="NotificationDeliveryStatus.Sending"/> row
    /// back to <see cref="NotificationDeliveryStatus.Pending"/>, allowing the next Wolverine retry
    /// to re-claim and re-attempt delivery.
    /// <para>
    /// Call this in a <c>catch (RetryableSmtpException)</c> block before rethrowing so that
    /// transient SMTP failures do not leave the row permanently stuck in <c>Sending</c>. Without
    /// this call the row would only recover after <see cref="StuckSendingThreshold"/> has elapsed,
    /// which is longer than Wolverine's retry schedule.
    /// </para>
    /// </summary>
    public Task MarkReadyAsync(Guid idempotencyKey, CancellationToken ct) =>
        db.NotificationLogs
            .Where(l => l.IdempotencyKey == idempotencyKey
                        && l.DeliveryStatus == NotificationDeliveryStatus.Sending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.DeliveryStatus, NotificationDeliveryStatus.Pending)
                .SetProperty(l => l.SendingClaimedAt, (DateTimeOffset?)null), ct);

    /// <summary>
/// Transitions the log row from <c>Sending</c> to <see cref="NotificationDeliveryStatus.Failed"/>,
/// recording that a permanent SMTP error occurred.  The Wolverine message will be moved to
/// the dead-letter queue by the caller rethrowing a <c>TerminalSmtpException</c>.
/// </summary>
public Task MarkFailedAsync(Guid idempotencyKey, CancellationToken ct) =>
    db.NotificationLogs
        .Where(l => l.IdempotencyKey == idempotencyKey
                    && l.DeliveryStatus == NotificationDeliveryStatus.Sending)
        .ExecuteUpdateAsync(s => s
            .SetProperty(l => l.DeliveryStatus, NotificationDeliveryStatus.Failed)
            .SetProperty(l => l.SendingClaimedAt, (DateTimeOffset?)null), ct);

/// <summary>Transitions the log row from Sending to Sent.</summary>
    public Task MarkSentAsync(Guid idempotencyKey, CancellationToken ct) =>
        db.NotificationLogs
            .Where(l => l.IdempotencyKey == idempotencyKey)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.DeliveryStatus, NotificationDeliveryStatus.Sent), ct);

    private Task<int> AtomicClaimAsync(Guid idempotencyKey, CancellationToken ct) =>
        db.NotificationLogs
            .Where(l => l.IdempotencyKey == idempotencyKey
                        && l.DeliveryStatus == NotificationDeliveryStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.DeliveryStatus, NotificationDeliveryStatus.Sending)
                .SetProperty(l => l.SendingClaimedAt, clock.UtcNow), ct);
}
