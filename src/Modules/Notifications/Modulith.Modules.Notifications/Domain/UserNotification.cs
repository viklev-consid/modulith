using Modulith.Shared.Kernel.Domain;

namespace Modulith.Modules.Notifications.Domain;

public sealed class UserNotification : Entity<UserNotificationId>
{
    private UserNotification() : base(new UserNotificationId(Guid.Empty)) { }

    private UserNotification(
        UserNotificationId id,
        Guid recipientUserId,
        string type,
        BellNotificationCategory category,
        BellNotificationSeverity severity,
        string title,
        string body,
        string? linkHref,
        string? linkLabel,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt,
        DateTimeOffset retentionUntil,
        Guid idempotencyKey) : base(id)
    {
        RecipientUserId = recipientUserId;
        Type = type;
        Category = category;
        Severity = severity;
        Title = title;
        Body = body;
        LinkHref = linkHref;
        LinkLabel = linkLabel;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        RetentionUntil = retentionUntil;
        IdempotencyKey = idempotencyKey;
    }

    public Guid RecipientUserId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public BellNotificationCategory Category { get; private set; }
    public BellNotificationSeverity Severity { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string? LinkHref { get; private set; }
    public string? LinkLabel { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset RetentionUntil { get; private set; }
    public Guid IdempotencyKey { get; private set; }

    public bool IsRead => ReadAt is not null;
    public bool IsArchived => ArchivedAt is not null;

    public static UserNotification Create(
        Guid recipientUserId,
        string type,
        BellNotificationCategory category,
        BellNotificationSeverity severity,
        string title,
        string body,
        string? linkHref,
        string? linkLabel,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt,
        DateTimeOffset retentionUntil,
        Guid idempotencyKey)
        => new(
            new UserNotificationId(Guid.NewGuid()),
            recipientUserId,
            type,
            category,
            severity,
            title,
            body,
            linkHref,
            linkLabel,
            createdAt,
            expiresAt,
            retentionUntil,
            idempotencyKey);

    public void MarkRead(DateTimeOffset readAt, DateTimeOffset retentionUntil)
    {
        ReadAt ??= readAt;
        RetentionUntil = retentionUntil;
    }

    public void Archive(DateTimeOffset archivedAt, DateTimeOffset retentionUntil)
    {
        ArchivedAt ??= archivedAt;
        RetentionUntil = retentionUntil;
    }
}
