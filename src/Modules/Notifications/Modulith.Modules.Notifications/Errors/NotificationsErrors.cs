using ErrorOr;

namespace Modulith.Modules.Notifications.Errors;

internal static class NotificationsErrors
{
    // Email delivery
    public static readonly Error EmailDeliveryFailed =
        Error.Failure("Notifications.Email.DeliveryFailed", "The notification email could not be delivered.");

    public static readonly Error RecipientInvalid =
        Error.Validation("Notifications.Email.RecipientInvalid", "The recipient email address is invalid.");

    // Idempotency
    public static readonly Error AlreadySent =
        Error.Conflict("Notifications.Notification.AlreadySent", "This notification has already been sent.");
}
