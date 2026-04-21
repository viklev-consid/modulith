namespace Modulith.Modules.Notifications.Templates;

internal static class EmailChangedTemplate
{
    public const string Subject = "Your email address has been changed";

    public static string HtmlBody(string newEmail) =>
        $"""
        <html>
        <body>
          <h1>Email address changed</h1>
          <p>The email address on your account has been changed to <strong>{newEmail}</strong>.</p>
          <p>If you did not make this change, contact support immediately — your account may be compromised.</p>
        </body>
        </html>
        """;

    public static string PlainTextBody(string newEmail) =>
        $"The email address on your account has been changed to {newEmail}. If you did not make this change, contact support immediately.";
}
