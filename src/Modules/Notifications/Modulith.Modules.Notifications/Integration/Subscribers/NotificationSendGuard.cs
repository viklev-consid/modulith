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
/// <see cref="NotificationLog.SendingClaimedAt"/> exceeds <see cref="stuckSendingThreshold"/>,
/// resets them to <c>Pending</c> (clearing the stale token), and re-claims with a fresh
/// token. This path is crash-recovery only — a row whose original sender is still legitimately
/// running will not be eligible because its <c>SendingClaimedAt</c> is recent, and even if
/// age-based reclaim were triggered the original sender's token would no longer match the
/// newly-issued one, making its subsequent <see cref="MarkSentAsync"/> a safe no-op.
/// </para>
/// <para>
/// DLQ replay recovery: if the process moved a message to the dead-letter queue (terminal
/// SMTP failure, <c>Sending → Failed</c>), an admin can replay the envelope via the DLQ
/// management endpoints after fixing the root cause. On replay <see cref="TryClaimAsync"/>
/// detects the <c>Failed</c> row, resets it to <c>Pending</c>, and re-claims, enabling a
/// fresh send attempt.
/// </para>
/// </summary>
public sealed class NotificationSendGuard(NotificationsDbContext db, IClock clock)
{
    /// <summary>Rows stuck in Sending for longer than this are eligible for automatic recovery.</summary>
    private static readonly TimeSpan stuckSendingThreshold = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan leaseRenewalInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Attempts to atomically claim the send slot for <paramref name="idempotencyKey"/>.
    /// </summary>
    /// <returns>
    /// The opaque lease token if the caller holds the exclusive claim and should proceed to
    /// send the email; <c>null</c> if another attempt is in-flight or the notification was
    /// already sent. Pass the non-null token to every subsequent guard call.
    /// </returns>
    public async Task<Guid?> TryClaimAsync(Guid idempotencyKey, CancellationToken ct)
    {
        // Fast path: row is Pending — claim it.
        var leaseToken = await AtomicClaimAsync(idempotencyKey, ct);
        if (leaseToken is not null)
        {
            return leaseToken;
        }

        // Crash recovery: row stuck in Sending due to process crash.
        // Reset to Pending after the stale threshold so the next attempt can re-claim.
        var staleThreshold = clock.UtcNow - stuckSendingThreshold;
        var staleRecovered = await db.NotificationLogs
            .Where(l => l.IdempotencyKey == idempotencyKey
                        && l.DeliveryStatus == NotificationDeliveryStatus.Sending
                        && l.SendingClaimedAt < staleThreshold)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.DeliveryStatus, NotificationDeliveryStatus.Pending)
                .SetProperty(l => l.SendingClaimedAt, (DateTimeOffset?)null)
                .SetProperty(l => l.SendingLeaseToken, (Guid?)null), ct);

        if (staleRecovered > 0)
        {
            NotificationsTelemetry.SendGuardStaleRecoveries.Add(1);
            return await AtomicClaimAsync(idempotencyKey, ct);
        }

        // DLQ replay recovery: row is Failed because a terminal SMTP error occurred.
        // An admin has replayed the envelope via the DLQ endpoint after fixing the root cause.
        // Reset to Pending so this attempt can claim and try again.
        var failedRecovered = await db.NotificationLogs
            .Where(l => l.IdempotencyKey == idempotencyKey
                        && l.DeliveryStatus == NotificationDeliveryStatus.Failed)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.DeliveryStatus, NotificationDeliveryStatus.Pending)
                .SetProperty(l => l.SendingClaimedAt, (DateTimeOffset?)null)
                .SetProperty(l => l.SendingLeaseToken, (Guid?)null), ct);

        if (failedRecovered > 0)
        {
            NotificationsTelemetry.SendGuardFailedRecoveries.Add(1);
            return await AtomicClaimAsync(idempotencyKey, ct);
        }

        return null; // Row is in-flight (Sending, recent) or already Sent.
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
    public async Task MarkReadyAsync(Guid idempotencyKey, Guid leaseToken, CancellationToken ct)
    {
        var affected = await db.NotificationLogs
            .Where(l => l.IdempotencyKey == idempotencyKey
                        && l.DeliveryStatus == NotificationDeliveryStatus.Sending
                        && l.SendingLeaseToken == leaseToken)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.DeliveryStatus, NotificationDeliveryStatus.Pending)
                .SetProperty(l => l.SendingClaimedAt, (DateTimeOffset?)null)
                .SetProperty(l => l.SendingLeaseToken, (Guid?)null), ct);

        if (affected > 0)
        {
            NotificationsTelemetry.EmailsFailedTransient.Add(1);
        }
    }

    /// <summary>
    /// Transitions <c>Sending → <see cref="NotificationDeliveryStatus.Failed"/></c>.
    /// Only applies when <paramref name="leaseToken"/> matches the stored token.
    /// Call this in a <c>catch (TerminalSmtpException)</c> block before rethrowing.
    /// </summary>
    public async Task MarkFailedAsync(Guid idempotencyKey, Guid leaseToken, CancellationToken ct)
    {
        var affected = await db.NotificationLogs
            .Where(l => l.IdempotencyKey == idempotencyKey
                        && l.DeliveryStatus == NotificationDeliveryStatus.Sending
                        && l.SendingLeaseToken == leaseToken)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.DeliveryStatus, NotificationDeliveryStatus.Failed)
                .SetProperty(l => l.SendingClaimedAt, (DateTimeOffset?)null)
                .SetProperty(l => l.SendingLeaseToken, (Guid?)null), ct);

        if (affected > 0)
        {
            NotificationsTelemetry.EmailsFailedTerminal.Add(1);
        }
    }

    /// <summary>
    /// Transitions <c>Sending → <see cref="NotificationDeliveryStatus.Sent"/></c>.
    /// Only applies when <paramref name="leaseToken"/> matches the stored token, so a
    /// late sender whose claim was superseded by crash-recovery cannot mark the row Sent.
    /// </summary>
    public async Task MarkSentAsync(Guid idempotencyKey, Guid leaseToken, CancellationToken ct)
    {
        var affected = await db.NotificationLogs
            .Where(l => l.IdempotencyKey == idempotencyKey
                        && l.DeliveryStatus == NotificationDeliveryStatus.Sending
                        && l.SendingLeaseToken == leaseToken)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.DeliveryStatus, NotificationDeliveryStatus.Sent)
                .SetProperty(l => l.SentAt, clock.UtcNow)
                .SetProperty(l => l.SendingLeaseToken, (Guid?)null), ct);

        if (affected > 0)
        {
            NotificationsTelemetry.EmailsSent.Add(1);
        }
    }

    public async Task SendWithLeaseRenewalAsync(
        Guid idempotencyKey,
        Guid leaseToken,
        Func<CancellationToken, Task> sendAsync,
        CancellationToken ct,
        TimeSpan? renewalInterval = null)
    {
        using var renewalCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var renewalTask = RenewLeaseUntilCancelledAsync(
            idempotencyKey,
            leaseToken,
            renewalInterval ?? leaseRenewalInterval,
            renewalCts.Token);

        try
        {
            await sendAsync(ct);
        }
        finally
        {
            await renewalCts.CancelAsync();
            await renewalTask;
        }
    }

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

        if (claimed > 0)
        {
            NotificationsTelemetry.SendGuardClaims.Add(1);
            return leaseToken;
        }

        return null;

    }

    private async Task RenewLeaseUntilCancelledAsync(
        Guid idempotencyKey,
        Guid leaseToken,
        TimeSpan renewalInterval,
        CancellationToken ct)
    {
        try
        {
            while (true)
            {
                await Task.Delay(renewalInterval, ct);
                await db.NotificationLogs
                    .Where(l => l.IdempotencyKey == idempotencyKey
                                && l.DeliveryStatus == NotificationDeliveryStatus.Sending
                                && l.SendingLeaseToken == leaseToken)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(l => l.SendingClaimedAt, clock.UtcNow), ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal completion: SMTP delivery ended, so lease renewal is no longer needed.
        }
    }
}
