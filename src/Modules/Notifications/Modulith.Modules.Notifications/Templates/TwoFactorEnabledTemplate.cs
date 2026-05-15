namespace Modulith.Modules.Notifications.Templates;

internal static class TwoFactorEnabledTemplate
{
    public const string Subject = "Two-factor authentication enabled";

    public static readonly string HtmlBody =
        """
        <html>
        <body>
          <h1>Two-factor authentication enabled</h1>
          <p>Two-factor authentication has been enabled for your account. If you did not make this change, reset your password and contact support immediately.</p>
        </body>
        </html>
        """;

    public static readonly string PlainTextBody =
        "Two-factor authentication has been enabled for your account. If you did not make this change, reset your password and contact support immediately.";
}
