namespace Modulith.Modules.Notifications.Templates;

internal static class ExternalLoginLinkedTemplate
{
    public const string Subject = "Google account linked";

    public static string HtmlBody(string provider) =>
        $"""
        <html>
        <body>
          <h1>External account linked</h1>
          <p>Your {provider} account has been successfully linked to your account.</p>
          <p>If you did not do this, please change your password immediately and contact support.</p>
        </body>
        </html>
        """;

    public static string PlainTextBody(string provider) =>
        $"Your {provider} account has been linked to your account. If you did not do this, change your password immediately and contact support.";
}
