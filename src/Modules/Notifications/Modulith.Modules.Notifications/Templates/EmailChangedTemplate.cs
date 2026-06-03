namespace Modulith.Modules.Notifications.Templates;

internal static class EmailChangedTemplate
{
    public const string Subject = "Your email address has been changed";

    public static string PlainTextBody(string newEmail) =>
        $"The email address on your account has been changed to {newEmail}. If you did not make this change, contact support immediately.";
}
