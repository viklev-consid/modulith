using Modulith.Shared.Kernel.Domain;

namespace Modulith.Modules.Notifications.Domain;

public sealed class NotificationLog : Entity<NotificationLogId>
{
    private NotificationLog() : base(new NotificationLogId(Guid.Empty)) { }

    private NotificationLog(
        NotificationLogId id,
        Guid userId,
        string recipientEmail,
        NotificationType notificationType,
        string subject,
        DateTimeOffset createdAt,
        Guid idempotencyKey) : base(id)
    {
        UserId = userId;
        RecipientEmail = recipientEmail;
        NotificationType = notificationType;
        Subject = subject;
        CreatedAt = createdAt;
        IdempotencyKey = idempotencyKey;
        DeliveryStatus = NotificationDeliveryStatus.Pending;
    }

    public Guid UserId { get; private set; }
    public string RecipientEmail { get; private set; } = string.Empty;
    public NotificationType NotificationType { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    /// <summary>Timestamp when the log row was created (before delivery).</summary>
    public DateTimeOffset CreatedAt { get; private set; }
    /// <summary>Timestamp when delivery was confirmed (<c>Sending → Sent</c>). Null until then.</summary>
    public DateTimeOffset? SentAt { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public NotificationDeliveryStatus DeliveryStatus { get; private set; }
    /// <summary>Timestamp at which the exclusive send claim was taken (Pending → Sending).
    /// Null until the claim is first acquired. Used to detect stuck Sending rows.</summary>
    public DateTimeOffset? SendingClaimedAt { get; private set; }

    /// <summary>Opaque token generated when the send claim is taken. Every transition
    /// (<c>MarkReady</c>, <c>MarkSentAsync</c>, <c>MarkFailedAsync</c>) must supply the matching
    /// token, ensuring that only the holder of the current claim can advance the row.
    /// Cleared whenever the row leaves the <c>Sending</c> state.</summary>
    public Guid? SendingLeaseToken { get; private set; }

    public static NotificationLog Create(
        Guid userId,
        string recipientEmail,
        NotificationType notificationType,
        string subject,
        DateTimeOffset createdAt,
        Guid idempotencyKey)
        => new(
            new NotificationLogId(Guid.NewGuid()),
            userId,
            recipientEmail,
            notificationType,
            subject,
            createdAt,
            idempotencyKey);
}
