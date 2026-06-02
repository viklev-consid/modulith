using System.Net;

namespace Modulith.Modules.Notifications.Templates;

internal static class EmailChangeRequestTemplate
{
    public const string Subject = "Confirm your email address change";

    public static string HtmlBody(string token, string confirmationUrl) =>
        $"""
        <html>
        <body>
          <h1>Confirm email address change</h1>
          <p>We received a request to change the email address on your account. Use the link below to confirm. It expires in 30 minutes.</p>
          <p><a href="{WebUtility.HtmlEncode(confirmationUrl)}">Confirm email address change</a></p>
          <p>If the link does not work, copy this token into the confirmation screen:</p>
          <p><code>{WebUtility.HtmlEncode(token)}</code></p>
          <p>If you did not make this request, you can safely ignore this email. Your email address will not change.</p>
        </body>
        </html>
        """;

    public static string PlainTextBody(string token, string confirmationUrl) =>
        $"Confirm your email address change here (expires in 30 minutes): {confirmationUrl}\n\nIf the link does not work, copy this token into the confirmation screen: {token}\n\nIf you did not request this, ignore this email.";
}
