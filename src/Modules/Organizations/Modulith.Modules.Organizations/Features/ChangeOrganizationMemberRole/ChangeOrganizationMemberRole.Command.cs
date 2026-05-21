using Modulith.Modules.Organizations.Domain;

namespace Modulith.Modules.Organizations.Features.ChangeOrganizationMemberRole;

public sealed record ChangeOrganizationMemberRoleCommand(OrganizationId OrganizationId, Guid UserId, string Role, Guid ChangedByUserId);
