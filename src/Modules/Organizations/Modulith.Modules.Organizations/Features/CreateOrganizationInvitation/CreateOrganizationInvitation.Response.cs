namespace Modulith.Modules.Organizations.Features.CreateOrganizationInvitation;

public sealed record CreateOrganizationInvitationResponse(Guid InvitationId, string Email, string Role, DateTimeOffset ExpiresAt, string RawToken);
