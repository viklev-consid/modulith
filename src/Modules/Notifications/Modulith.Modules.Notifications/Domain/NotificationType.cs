namespace Modulith.Modules.Notifications.Domain;

public enum NotificationType
{
    WelcomeEmail = 1,
    PasswordResetRequest = 2,
    PasswordResetConfirmation = 3,
    PasswordChanged = 4,
    EmailChangeRequest = 5,
    EmailChanged = 6,
    TwoFactorEnabled = 11,
    TwoFactorDisabled = 12,
    RecoveryCodesRegenerated = 13,
    EmailConfirmationRequest = 14,
    UserInvitation = 15,
    OrganizationInvitation = 16,
}
