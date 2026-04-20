namespace Modulith.Modules.Notifications.Templates;

internal static class WelcomeEmailTemplate
{
    public const string Subject = "Welcome to Modulith!";

    public static string HtmlBody(string displayName) =>
        $"""
        <html>
        <body>
          <h1>Welcome, {displayName}!</h1>
          <p>Your account has been created. You can now sign in and start using the platform.</p>
        </body>
        </html>
        """;

    public static string PlainTextBody(string displayName) =>
        $"Welcome, {displayName}! Your account has been created. You can now sign in and start using the platform.";
}
