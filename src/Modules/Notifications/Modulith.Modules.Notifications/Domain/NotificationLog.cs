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
        DateTimeOffset sentAt) : base(id)
    {
        UserId = userId;
        RecipientEmail = recipientEmail;
        NotificationType = notificationType;
        Subject = subject;
        SentAt = sentAt;
    }

    public Guid UserId { get; private set; }
    public string RecipientEmail { get; private set; } = string.Empty;
    public NotificationType NotificationType { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public DateTimeOffset SentAt { get; private set; }

    public static NotificationLog Create(
        Guid userId,
        string recipientEmail,
        NotificationType notificationType,
        string subject,
        DateTimeOffset sentAt)
        => new(
            new NotificationLogId(Guid.NewGuid()),
            userId,
            recipientEmail,
            notificationType,
            subject,
            sentAt);
}
