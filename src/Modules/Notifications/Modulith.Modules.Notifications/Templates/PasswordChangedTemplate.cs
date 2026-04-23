namespace Modulith.Modules.Notifications.Templates;

internal static class PasswordChangedTemplate
{
    public const string Subject = "Your password has been changed";

    public static readonly string HtmlBody =
        """
        <html>
        <body>
          <h1>Password changed</h1>
          <p>Your account password has been changed. If you did not make this change, contact support immediately and consider resetting your password.</p>
        </body>
        </html>
        """;

    public static readonly string PlainTextBody =
        "Your account password has been changed. If you did not make this change, contact support immediately.";
}
