using Modulith.Shared.Kernel.Domain;

namespace Modulith.Modules.Notifications.Domain;

public sealed class NotificationPreference : Entity<NotificationPreferenceId>
{
    private NotificationPreference() : base(new NotificationPreferenceId(Guid.Empty)) { }

    private NotificationPreference(
        NotificationPreferenceId id,
        Guid userId,
        BellNotificationCategory category,
        bool bellEnabled,
        bool emailEnabled,
        DateTimeOffset updatedAt) : base(id)
    {
        UserId = userId;
        Category = category;
        BellEnabled = bellEnabled;
        EmailEnabled = emailEnabled;
        UpdatedAt = updatedAt;
    }

    public Guid UserId { get; private set; }
    public BellNotificationCategory Category { get; private set; }
    public bool BellEnabled { get; private set; }
    public bool EmailEnabled { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static NotificationPreference Create(
        Guid userId,
        BellNotificationCategory category,
        bool bellEnabled,
        bool emailEnabled,
        DateTimeOffset updatedAt)
        => new(
            new NotificationPreferenceId(Guid.NewGuid()),
            userId,
            category,
            bellEnabled,
            emailEnabled,
            updatedAt);

    public void Update(bool bellEnabled, bool emailEnabled, DateTimeOffset updatedAt)
    {
        BellEnabled = bellEnabled;
        EmailEnabled = emailEnabled;
        UpdatedAt = updatedAt;
    }
}
