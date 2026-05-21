namespace Modulith.Modules.Organizations.Features.AcceptOrganizationInvitation;

public sealed record AcceptOrganizationInvitationCommand(string InvitationToken, Guid UserId, string Email);
