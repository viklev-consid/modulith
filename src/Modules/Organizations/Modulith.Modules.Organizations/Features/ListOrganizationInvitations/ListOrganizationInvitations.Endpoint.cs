using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Modules.Organizations.Authorization;
using Modulith.Modules.Organizations.Contracts.Authorization;
using Modulith.Shared.Infrastructure.Authorization;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Organizations.Features.ListOrganizationInvitations;

internal static class ListOrganizationInvitationsEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet(OrganizationsRoutes.Invitations,
            async (
                string organizationRef,
                IOrganizationRefResolver resolver,
                IScopedAuthorizationService<OrganizationScope> authorization,
                ICurrentUser currentUser,
                IMessageBus bus,
                CancellationToken ct,
                int page = 1,
                int pageSize = 20) =>
            {
                var organization = await resolver.ResolveAsync(organizationRef, ct);
                if (organization.IsError)
                {
                    return organization.ToProblemDetailsOr(_ => Results.Empty);
                }

                var access = await authorization.AuthorizeAsync(currentUser, organization.Value, OrganizationsPermissions.InvitationsManage, ScopedAuthorizationOptions.WithPlatformOverride, ct);
                if (!access.Succeeded)
                {
                    return Results.Forbid();
                }

                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<ListOrganizationInvitationsResponse>>(
                    new ListOrganizationInvitationsQuery(organization.Value.Id, page, pageSize),
                    ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("ListOrganizationInvitations")
        .WithSummary("List organization invitations.")
        .Produces<ListOrganizationInvitationsResponse>()
        .RequireAuthorization();
}
