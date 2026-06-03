namespace Modulith.Modules.Notifications.Templates;

internal static class EmailConfirmationRequestTemplate
{
    public const string Subject = "Confirm your email address";

    public static string PlainTextBody(string displayName, string token, string confirmationUrl) =>
        $"Hi {displayName},\n\nConfirm your email address to finish creating your account. This link expires in 24 hours: {confirmationUrl}\n\nIf the link does not work, copy this token into the confirmation screen: {token}\n\nIf you did not create an account, ignore this email.";
}
