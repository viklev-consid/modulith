namespace Modulith.Modules.Notifications.Templates;

internal static class PasswordResetRequestTemplate
{
    public const string Subject = "Reset your password";

    public static string PlainTextBody(string token, string resetUrl) =>
        $"We received a request to reset your password. Reset here (expires in 30 minutes): {resetUrl}\n\nIf the link does not work, copy this token into the reset screen: {token}\n\nIf you did not request this, ignore this email.";
}
