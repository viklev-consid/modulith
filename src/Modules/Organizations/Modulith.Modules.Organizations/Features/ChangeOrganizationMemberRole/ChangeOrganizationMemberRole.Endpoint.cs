using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Modules.Organizations.Authorization;
using Modulith.Modules.Organizations.Contracts.Authorization;
using Modulith.Shared.Infrastructure.Authorization;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Organizations.Features.ChangeOrganizationMemberRole;

internal static class ChangeOrganizationMemberRoleEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPut(OrganizationsRoutes.MemberRole,
            async (
                string organizationRef,
                Guid userId,
                ChangeOrganizationMemberRoleRequest request,
                IValidator<ChangeOrganizationMemberRoleRequest> validator,
                IOrganizationRefResolver resolver,
                IScopedAuthorizationService<OrganizationScope> authorization,
                ICurrentUser currentUser,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                if (currentUser.Id is null || !Guid.TryParse(currentUser.Id, out var changedByUserId))
                {
                    return Results.Unauthorized();
                }

                var validation = await validator.ValidateAsync(request, ct);
                if (!validation.IsValid)
                {
                    return Results.ValidationProblem(validation.ToDictionary(), statusCode: StatusCodes.Status422UnprocessableEntity);
                }

                var organization = await resolver.ResolveAsync(organizationRef, ct);
                if (organization.IsError)
                {
                    return organization.ToProblemDetailsOr(_ => Results.Empty);
                }

                var access = await authorization.AuthorizeAsync(currentUser, organization.Value, OrganizationsPermissions.MembersManage, ScopedAuthorizationOptions.WithPlatformOverride, ct);
                if (!access.Succeeded)
                {
                    return Results.Forbid();
                }

                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<ChangeOrganizationMemberRoleResponse>>(
                    new ChangeOrganizationMemberRoleCommand(organization.Value.Id, userId, request.Role, changedByUserId),
                    ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("ChangeOrganizationMemberRole")
        .WithSummary("Change an organization member role.")
        .Produces<ChangeOrganizationMemberRoleResponse>()
        .RequireAuthorization();
}
