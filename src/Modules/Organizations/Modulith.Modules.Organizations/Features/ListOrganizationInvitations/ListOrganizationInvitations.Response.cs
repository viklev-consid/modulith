namespace Modulith.Modules.Organizations.Features.ListOrganizationInvitations;

public sealed record ListOrganizationInvitationsResponse(
    IReadOnlyCollection<OrganizationInvitationItem> Invitations,
    int Page,
    int PageSize,
    int Total);

public sealed record OrganizationInvitationItem(Guid InvitationId, string Email, string Role, DateTimeOffset ExpiresAt, bool IsPending);
