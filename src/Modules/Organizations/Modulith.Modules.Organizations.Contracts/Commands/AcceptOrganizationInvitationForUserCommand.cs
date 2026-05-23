using ErrorOr;

namespace Modulith.Modules.Organizations.Contracts.Commands;

public sealed record AcceptOrganizationInvitationForUserCommand(
    string InvitationToken,
    Guid UserId,
    string Email);

public sealed record AcceptedOrganizationInvitationForUserResponse(
    Guid OrganizationId,
    string Role);
