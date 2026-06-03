namespace Modulith.Modules.Notifications.Templates;

public sealed record OrganizationInvitationModel(
    string Role,
    string Token,
    string InvitationUrl);
