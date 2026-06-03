namespace Modulith.Modules.Notifications.Templates;

internal static class UserInvitationTemplate
{
    public const string Subject = "You're invited to join";

    public static string PlainTextBody(string token, string invitationUrl) =>
        $"You're invited. Accept the invitation here: {invitationUrl}\n\nIf the link does not work, copy this token into the invitation screen: {token}\n\nIf you did not expect this invitation, you can ignore this email.";
}
