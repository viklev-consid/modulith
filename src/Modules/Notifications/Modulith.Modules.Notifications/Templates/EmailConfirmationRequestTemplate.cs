using System.Net;

namespace Modulith.Modules.Notifications.Templates;

internal static class EmailConfirmationRequestTemplate
{
    public const string Subject = "Confirm your email address";

    public static string HtmlBody(string displayName, string token, string confirmationUrl) =>
        $"""
        <html>
          <body>
            <p>Hi {WebUtility.HtmlEncode(displayName)},</p>
            <p>Confirm your email address to finish creating your account. This link expires in 24 hours.</p>
            <p><a href="{WebUtility.HtmlEncode(confirmationUrl)}">Confirm email address</a></p>
            <p>If the link does not work, copy this token into the confirmation screen:</p>
            <p><code>{WebUtility.HtmlEncode(token)}</code></p>
            <p>If you did not create an account, ignore this email.</p>
          </body>
        </html>
        """;

    public static string PlainTextBody(string displayName, string token, string confirmationUrl) =>
        $"Hi {displayName},\n\nConfirm your email address to finish creating your account. This link expires in 24 hours: {confirmationUrl}\n\nIf the link does not work, copy this token into the confirmation screen: {token}\n\nIf you did not create an account, ignore this email.";
}
