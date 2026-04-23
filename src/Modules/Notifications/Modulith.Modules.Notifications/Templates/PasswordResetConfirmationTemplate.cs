namespace Modulith.Modules.Notifications.Templates;

internal static class PasswordResetConfirmationTemplate
{
    public const string Subject = "Your password has been reset";

    public static readonly string HtmlBody =
        """
        <html>
        <body>
          <h1>Password reset successful</h1>
          <p>Your password has been reset. If you did not make this change, contact support immediately.</p>
        </body>
        </html>
        """;

    public static readonly string PlainTextBody =
        "Your password has been reset. If you did not make this change, contact support immediately.";
}
