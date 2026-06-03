namespace Modulith.Modules.Notifications.Templates;

internal static class PasswordChangedTemplate
{
    public const string Subject = "Your password has been changed";

    public static readonly string PlainTextBody =
        "Your account password has been changed. If you did not make this change, contact support immediately.";
}
