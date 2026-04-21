namespace Modulith.Modules.Notifications.Templates;

internal static class EmailChangeRequestTemplate
{
    public const string Subject = "Confirm your email address change";

    public static string HtmlBody(string token) =>
        $"""
        <html>
        <body>
          <h1>Confirm email address change</h1>
          <p>We received a request to change the email address on your account. Use the token below to confirm. It expires in 30 minutes.</p>
          <p><code>{token}</code></p>
          <p>If you did not make this request, you can safely ignore this email. Your email address will not change.</p>
        </body>
        </html>
        """;

    public static string PlainTextBody(string token) =>
        $"Confirm your email address change with this token (expires in 30 minutes): {token}. If you did not request this, ignore this email.";
}
