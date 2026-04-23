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
        DateTimeOffset sentAt,
        Guid idempotencyKey) : base(id)
    {
        UserId = userId;
        RecipientEmail = recipientEmail;
        NotificationType = notificationType;
        Subject = subject;
        SentAt = sentAt;
        IdempotencyKey = idempotencyKey;
    }

    public Guid UserId { get; private set; }
    public string RecipientEmail { get; private set; } = string.Empty;
    public NotificationType NotificationType { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public DateTimeOffset SentAt { get; private set; }
    public Guid IdempotencyKey { get; private set; }

    public static NotificationLog Create(
        Guid userId,
        string recipientEmail,
        NotificationType notificationType,
        string subject,
        DateTimeOffset sentAt,
        Guid idempotencyKey)
        => new(
            new NotificationLogId(Guid.NewGuid()),
            userId,
            recipientEmail,
            notificationType,
            subject,
            sentAt,
            idempotencyKey);
}
