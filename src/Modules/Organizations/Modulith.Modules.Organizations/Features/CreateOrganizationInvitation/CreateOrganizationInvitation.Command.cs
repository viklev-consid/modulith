using Modulith.Modules.Organizations.Domain;

namespace Modulith.Modules.Organizations.Features.CreateOrganizationInvitation;

public sealed record CreateOrganizationInvitationCommand(OrganizationId OrganizationId, string Email, string Role, Guid InvitedByUserId);
