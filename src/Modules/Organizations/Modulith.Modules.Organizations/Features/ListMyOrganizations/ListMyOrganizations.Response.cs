namespace Modulith.Modules.Organizations.Features.ListMyOrganizations;

public sealed record ListMyOrganizationsResponse(IReadOnlyCollection<MyOrganizationItem> Organizations);

public sealed record MyOrganizationItem(
    Guid OrganizationId,
    string Name,
    string Slug,
    string Role,
    IReadOnlyCollection<string> Permissions,
    string PermissionsVersion);
