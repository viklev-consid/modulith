namespace Modulith.Modules.Notifications.Templates;

internal static class PasswordResetRequestTemplate
{
    public const string Subject = "Reset your password";

    public static string HtmlBody(string token) =>
        $"""
        <html>
        <body>
          <h1>Password reset request</h1>
          <p>We received a request to reset the password for your account. Use the token below to complete the reset. It expires in 30 minutes.</p>
          <p><code>{token}</code></p>
          <p>If you did not request a password reset, you can safely ignore this email.</p>
        </body>
        </html>
        """;

    public static string PlainTextBody(string token) =>
        $"We received a request to reset your password. Your reset token (expires in 30 minutes): {token}\n\nIf you did not request this, ignore this email.";
}
