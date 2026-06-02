using ErrorOr;

namespace Modulith.Modules.Notifications.Errors;

internal static class NotificationsErrors
{
    // Email delivery
    public static readonly Error EmailDeliveryFailed =
        Error.Failure("Notifications.Email.DeliveryFailed", "The notification email could not be delivered.");

    public static readonly Error RecipientInvalid =
        Error.Validation("Notifications.Email.RecipientInvalid", "The recipient email address is invalid.");

    public static readonly Error NotificationTypeRequired =
        Error.Validation("Notifications.Bell.TypeRequired", "The notification type is required.");

    public static readonly Error NotificationTitleRequired =
        Error.Validation("Notifications.Bell.TitleRequired", "The notification title is required.");

    public static readonly Error NotificationInvalid =
        Error.Validation("Notifications.Bell.Invalid", "The notification contains invalid values.");

    public static readonly Error NotificationNotFound =
        Error.NotFound("Notifications.Bell.NotFound", "The notification was not found.");

    public static readonly Error NotificationPreferenceInvalid =
        Error.Validation("Notifications.Preferences.Invalid", "The notification preference is invalid.");

    public static readonly Error TooManyNotificationStreams =
        Error.Conflict("Notifications.Stream.TooManyActiveStreams", "Too many notification streams are already active for this user.");

    // Idempotency
    public static readonly Error AlreadySent =
        Error.Conflict("Notifications.Notification.AlreadySent", "This notification has already been sent.");
}
