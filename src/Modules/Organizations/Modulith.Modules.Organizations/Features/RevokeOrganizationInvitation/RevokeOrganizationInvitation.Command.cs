using Modulith.Modules.Organizations.Domain;

namespace Modulith.Modules.Organizations.Features.RevokeOrganizationInvitation;

public sealed record RevokeOrganizationInvitationCommand(OrganizationId OrganizationId, OrganizationInvitationId InvitationId, Guid RevokedByUserId);
