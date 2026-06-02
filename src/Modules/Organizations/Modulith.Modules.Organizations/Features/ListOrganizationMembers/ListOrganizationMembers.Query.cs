using Modulith.Modules.Organizations.Domain;

namespace Modulith.Modules.Organizations.Features.ListOrganizationMembers;

public sealed record ListOrganizationMembersQuery(OrganizationId OrganizationId, int Page = 1, int PageSize = 20);
