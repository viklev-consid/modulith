namespace Modulith.Modules.Notifications.Templates;

internal static class ExternalLoginPendingExistingUserTemplate
{
    public const string Subject = "Confirm linking your Google account";

    public static string HtmlBody(string token, string confirmationUrl) =>
        $"""
        <html>
        <body>
          <h1>Confirm linking your Google account</h1>
          <p>A request was made to link a Google account to your existing account. Use the link below to confirm. It expires in 15 minutes.</p>
          <p><a href="{confirmationUrl}">Confirm Google account link</a></p>
          <p>If the link does not work, copy this token into the confirmation screen:</p>
          <p><code>{token}</code></p>
          <p>If you did not request this, you can safely ignore this email.</p>
        </body>
        </html>
        """;

    public static string PlainTextBody(string token, string confirmationUrl) =>
        $"A request was made to link a Google account to your existing account. Confirm here (expires in 15 minutes): {confirmationUrl}\n\nIf the link does not work, copy this token into the confirmation screen: {token}\n\nIf you did not request this, ignore this email.";
}
