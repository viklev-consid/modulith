using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Modules.Organizations.Authorization;
using Modulith.Modules.Organizations.Contracts.Authorization;
using Modulith.Shared.Infrastructure.Authorization;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Organizations.Features.GetOrganizationAudit;

internal static class GetOrganizationAuditEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet(OrganizationsRoutes.Audit,
            async (
                string organizationRef,
                IOrganizationRefResolver resolver,
                IScopedAuthorizationService<OrganizationScope> authorization,
                ICurrentUser currentUser,
                CancellationToken ct) =>
            {
                var organization = await resolver.ResolveAsync(organizationRef, ct);
                if (organization.IsError)
                {
                    return organization.ToProblemDetailsOr(_ => Results.Empty);
                }

                var access = await authorization.AuthorizeAsync(
                    currentUser,
                    organization.Value,
                    OrganizationsPermissions.AuditRead,
                    ScopedAuthorizationOptions.WithPlatformOverride,
                    ct);
                if (!access.Succeeded)
                {
                    return Results.Forbid();
                }

                return Results.Ok(new GetOrganizationAuditResponse(
                    organization.Value.Id.Value,
                    access.AccessMode.ToString()));
            })
        .WithName("GetOrganizationAudit")
        .WithSummary("Get organization audit metadata. Full audit projection is owned by the Audit module.")
        .Produces<GetOrganizationAuditResponse>()
        .RequireAuthorization();
}
