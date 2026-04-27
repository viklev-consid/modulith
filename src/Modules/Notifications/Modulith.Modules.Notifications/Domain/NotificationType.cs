namespace Modulith.Modules.Notifications.Domain;

public enum NotificationType
{
    WelcomeEmail = 1,
    PasswordResetRequest = 2,
    PasswordResetConfirmation = 3,
    PasswordChanged = 4,
    EmailChangeRequest = 5,
    EmailChanged = 6,
    ExternalLoginPendingNewUser = 7,
    ExternalLoginPendingExistingUser = 8,
    ExternalLoginLinked = 9,
    ExternalLoginUnlinked = 10,
}
