namespace Modulith.Modules.Organizations.Features.ListOrganizationInvitations;

public sealed record ListOrganizationInvitationsResponse(IReadOnlyCollection<OrganizationInvitationItem> Invitations);

public sealed record OrganizationInvitationItem(Guid InvitationId, string Email, string Role, DateTimeOffset ExpiresAt, bool IsPending);
