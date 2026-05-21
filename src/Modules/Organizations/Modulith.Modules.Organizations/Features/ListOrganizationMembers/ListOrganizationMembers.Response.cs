namespace Modulith.Modules.Organizations.Features.ListOrganizationMembers;

public sealed record ListOrganizationMembersResponse(IReadOnlyCollection<OrganizationMemberItem> Members);

public sealed record OrganizationMemberItem(Guid UserId, string Role, DateTimeOffset JoinedAt, bool IsAnonymized);
