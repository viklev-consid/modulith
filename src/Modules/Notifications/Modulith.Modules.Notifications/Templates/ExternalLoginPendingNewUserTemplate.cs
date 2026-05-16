namespace Modulith.Modules.Notifications.Templates;

internal static class ExternalLoginPendingNewUserTemplate
{
    public const string Subject = "Confirm your new account";

    public static string HtmlBody(string token, string confirmationUrl) =>
        $"""
        <html>
        <body>
          <h1>Confirm your new account</h1>
          <p>You signed in with Google for the first time. Use the link below to confirm your account creation. It expires in 15 minutes.</p>
          <p><a href="{confirmationUrl}">Continue creating your account</a></p>
          <p>If the link does not work, copy this token into the confirmation screen:</p>
          <p><code>{token}</code></p>
          <p>If you did not attempt to sign in, you can safely ignore this email.</p>
        </body>
        </html>
        """;

    public static string PlainTextBody(string token, string confirmationUrl) =>
        $"You signed in with Google for the first time. Continue creating your account here (expires in 15 minutes): {confirmationUrl}\n\nIf the link does not work, copy this token into the confirmation screen: {token}\n\nIf you did not attempt to sign in, ignore this email.";
}
