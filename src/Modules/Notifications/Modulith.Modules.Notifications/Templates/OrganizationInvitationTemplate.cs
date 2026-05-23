using System.Net;

namespace Modulith.Modules.Notifications.Templates;

internal static class OrganizationInvitationTemplate
{
    public const string Subject = "You're invited to an organization";

    public static string HtmlBody(string role, string token, string invitationUrl) =>
        $"""
        <html>
        <body>
          <h1>Organization invitation</h1>
          <p>You've been invited to join an organization as <strong>{WebUtility.HtmlEncode(role)}</strong>.</p>
          <p><a href="{WebUtility.HtmlEncode(invitationUrl)}">Accept organization invitation</a></p>
          <p>If the link does not work, copy this token into the invitation screen:</p>
          <p><code>{WebUtility.HtmlEncode(token)}</code></p>
          <p>If you did not expect this invitation, you can ignore this email.</p>
        </body>
        </html>
        """;

    public static string PlainTextBody(string role, string token, string invitationUrl) =>
        $"You've been invited to join an organization as {role}. Accept the invitation here: {invitationUrl}\n\nIf the link does not work, copy this token into the invitation screen: {token}\n\nIf you did not expect this invitation, you can ignore this email.";
}
