using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Modules.Organizations.Authorization;
using Modulith.Modules.Organizations.Contracts.Authorization;
using Modulith.Modules.Organizations.Errors;
using Modulith.Shared.Infrastructure.Authorization;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Organizations.Features.RemoveOrganizationMember;

internal static class RemoveOrganizationMemberEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete(OrganizationsRoutes.MemberByUserId,
            async (
                string organizationRef,
                Guid userId,
                IOrganizationRefResolver resolver,
                IScopedAuthorizationService<OrganizationScope> authorization,
                ICurrentUser currentUser,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                if (currentUser.Id is null || !Guid.TryParse(currentUser.Id, out var removedByUserId))
                {
                    return Results.Unauthorized();
                }

                var organization = await resolver.ResolveAsync(organizationRef, ct);
                if (organization.IsError)
                {
                    return organization.ToProblemDetailsOr(_ => Results.Empty);
                }

                var permission = GetRequiredPermission(userId, removedByUserId);
                var access = await authorization.AuthorizeAsync(currentUser, organization.Value, permission, ScopedAuthorizationOptions.WithPlatformOverride, ct);
                if (!access.Succeeded)
                {
                    return Results.Forbid();
                }
                if (access.AccessMode == ScopedAuthorizationAccessMode.PlatformOverride)
                {
                    return Results.Problem(title: "Forbidden", detail: OrganizationsErrors.PlatformOverrideMutationForbidden.Description, statusCode: StatusCodes.Status403Forbidden);
                }

                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<ErrorOr.Success>>(
                    new RemoveOrganizationMemberCommand(organization.Value.Id, userId, removedByUserId),
                    ct);
                return result.ToProblemDetailsOr(_ => Results.NoContent());
            })
        .WithName("RemoveOrganizationMember")
        .WithSummary("Remove a member or leave an organization.")
        .Produces(StatusCodes.Status204NoContent)
        .RequireAuthorization();

    private static string GetRequiredPermission(Guid targetUserId, Guid actorUserId) =>
        targetUserId == actorUserId
            ? OrganizationsPermissions.OrganizationsRead
            : OrganizationsPermissions.MembersManage;
}
