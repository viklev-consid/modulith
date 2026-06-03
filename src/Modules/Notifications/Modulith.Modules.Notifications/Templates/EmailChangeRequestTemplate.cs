namespace Modulith.Modules.Notifications.Templates;

internal static class EmailChangeRequestTemplate
{
    public const string Subject = "Confirm your email address change";

    public static string PlainTextBody(string token, string confirmationUrl) =>
        $"Confirm your email address change here (expires in 30 minutes): {confirmationUrl}\n\nIf the link does not work, copy this token into the confirmation screen: {token}\n\nIf you did not request this, ignore this email.";
}
