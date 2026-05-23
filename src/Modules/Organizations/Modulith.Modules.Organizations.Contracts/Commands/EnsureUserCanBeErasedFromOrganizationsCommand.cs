namespace Modulith.Modules.Organizations.Contracts.Commands;

public sealed record EnsureUserCanBeErasedFromOrganizationsCommand(Guid UserId);

public sealed record EnsureUserCanBeErasedFromOrganizationsResponse(
    IReadOnlyCollection<UserErasureBlockingOrganization> BlockingOrganizations)
{
    public bool CanBeErased => BlockingOrganizations.Count == 0;
}

public sealed record UserErasureBlockingOrganization(
    Guid OrganizationId,
    string Name,
    string Slug,
    string Role,
    bool IsSoleOwner);
