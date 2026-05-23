using Modulith.Modules.Organizations.Domain;

namespace Modulith.Modules.Organizations.Features.RemoveOrganizationMember;

public sealed record RemoveOrganizationMemberCommand(OrganizationId OrganizationId, Guid UserId, Guid RemovedByUserId);
