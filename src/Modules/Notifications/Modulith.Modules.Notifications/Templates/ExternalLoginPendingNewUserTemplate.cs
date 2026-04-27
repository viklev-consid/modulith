namespace Modulith.Modules.Notifications.Templates;

internal static class ExternalLoginPendingNewUserTemplate
{
    public const string Subject = "Confirm your new account";

    public static string HtmlBody(string token) =>
        $"""
        <html>
        <body>
          <h1>Confirm your new account</h1>
          <p>You signed in with Google for the first time. Use the token below to confirm your account creation. It expires in 15 minutes.</p>
          <p><code>{token}</code></p>
          <p>If you did not attempt to sign in, you can safely ignore this email.</p>
        </body>
        </html>
        """;

    public static string PlainTextBody(string token) =>
        $"You signed in with Google for the first time. Confirm your account with this token (expires in 15 minutes): {token}\n\nIf you did not attempt to sign in, ignore this email.";
}
