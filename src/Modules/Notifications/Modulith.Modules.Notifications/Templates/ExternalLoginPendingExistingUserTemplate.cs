namespace Modulith.Modules.Notifications.Templates;

internal static class ExternalLoginPendingExistingUserTemplate
{
    public const string Subject = "Confirm linking your Google account";

    public static string HtmlBody(string token) =>
        $"""
        <html>
        <body>
          <h1>Confirm linking your Google account</h1>
          <p>A request was made to link a Google account to your existing account. Use the token below to confirm. It expires in 15 minutes.</p>
          <p><code>{token}</code></p>
          <p>If you did not request this, you can safely ignore this email.</p>
        </body>
        </html>
        """;

    public static string PlainTextBody(string token) =>
        $"A request was made to link a Google account to your existing account. Confirm with this token (expires in 15 minutes): {token}\n\nIf you did not request this, ignore this email.";
}
