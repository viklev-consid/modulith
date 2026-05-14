namespace Modulith.Modules.Notifications.Templates;

internal static class RecoveryCodesRegeneratedTemplate
{
    public const string Subject = "Recovery codes regenerated";

    public static readonly string HtmlBody =
        """
        <html>
        <body>
          <h1>Recovery codes regenerated</h1>
          <p>Your two-factor recovery codes have been regenerated. If you did not make this change, reset your password and contact support immediately.</p>
        </body>
        </html>
        """;

    public static readonly string PlainTextBody =
        "Your two-factor recovery codes have been regenerated. If you did not make this change, reset your password and contact support immediately.";
}
