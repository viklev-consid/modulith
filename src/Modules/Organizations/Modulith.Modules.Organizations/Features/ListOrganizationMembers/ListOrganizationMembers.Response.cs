namespace Modulith.Modules.Organizations.Features.ListOrganizationMembers;

public sealed record ListOrganizationMembersResponse(
    IReadOnlyCollection<OrganizationMemberItem> Members,
    int Page,
    int PageSize,
    int Total);

public sealed record OrganizationMemberItem(
    Guid? UserId,
    string Role,
    DateTimeOffset JoinedAt,
    bool IsAnonymized,
    string? DisplayName,
    string? Email);
