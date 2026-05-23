using Modulith.Modules.Organizations.Contracts.Authorization;
using Modulith.Shared.Infrastructure.Authorization;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Organizations.Authorization;

internal static class OrganizationEndpointAuthorization
{
    public static async Task<ScopedAuthorizationResult> AuthorizeAsync(
        this IScopedAuthorizationService<OrganizationScope> authorization,
        ICurrentUser currentUser,
        OrganizationRef organization,
        string permission,
        ScopedAuthorizationOptions options,
        CancellationToken ct) =>
        await authorization.AuthorizeAsync(
            currentUser,
            new OrganizationScope(organization.Id.Value),
            permission,
            options,
            ct);
}
