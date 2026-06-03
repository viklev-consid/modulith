namespace Modulith.Modules.Notifications.Templates;

internal static class TwoFactorDisabledTemplate
{
    public const string Subject = "Two-factor authentication disabled";

    public static readonly string PlainTextBody =
        "Two-factor authentication has been disabled for your account. If you did not make this change, reset your password and contact support immediately.";
}
