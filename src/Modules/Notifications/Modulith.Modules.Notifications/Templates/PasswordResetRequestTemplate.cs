using System.Net;

namespace Modulith.Modules.Notifications.Templates;

internal static class PasswordResetRequestTemplate
{
    public const string Subject = "Reset your password";

    public static string HtmlBody(string token, string resetUrl) =>
        $"""
        <html>
        <body>
          <h1>Password reset request</h1>
          <p>We received a request to reset the password for your account. Use the link below to complete the reset. It expires in 30 minutes.</p>
          <p><a href="{WebUtility.HtmlEncode(resetUrl)}">Reset your password</a></p>
          <p>If the link does not work, copy this token into the reset screen:</p>
          <p><code>{WebUtility.HtmlEncode(token)}</code></p>
          <p>If you did not request a password reset, you can safely ignore this email.</p>
        </body>
        </html>
        """;

    public static string PlainTextBody(string token, string resetUrl) =>
        $"We received a request to reset your password. Reset here (expires in 30 minutes): {resetUrl}\n\nIf the link does not work, copy this token into the reset screen: {token}\n\nIf you did not request this, ignore this email.";
}
