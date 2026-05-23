using System.Net;

namespace Modulith.Modules.Notifications.Templates;

internal static class UserInvitationTemplate
{
    public const string Subject = "You're invited to join";

    public static string HtmlBody(string token, string invitationUrl) =>
        $"""
        <html>
        <body>
          <h1>You're invited</h1>
          <p>Use the link below to create your account and accept the invitation.</p>
          <p><a href="{WebUtility.HtmlEncode(invitationUrl)}">Accept invitation</a></p>
          <p>If the link does not work, copy this token into the invitation screen:</p>
          <p><code>{WebUtility.HtmlEncode(token)}</code></p>
          <p>If you did not expect this invitation, you can ignore this email.</p>
        </body>
        </html>
        """;

    public static string PlainTextBody(string token, string invitationUrl) =>
        $"You're invited. Accept the invitation here: {invitationUrl}\n\nIf the link does not work, copy this token into the invitation screen: {token}\n\nIf you did not expect this invitation, you can ignore this email.";
}
