using FluentValidation;
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

namespace Modulith.Modules.Organizations.Features.UpdateOrganization;

internal static class UpdateOrganizationEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPatch(OrganizationsRoutes.ByRef,
            async (
                string organizationRef,
                UpdateOrganizationRequest request,
                IValidator<UpdateOrganizationRequest> validator,
                IOrganizationRefResolver resolver,
                IScopedAuthorizationService<OrganizationScope> authorization,
                ICurrentUser currentUser,
                IMessageBus bus,
                CancellationToken ct) =>
            {
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

                var access = await authorization.AuthorizeAsync(
                    currentUser,
                    organization.Value,
                    OrganizationsPermissions.OrganizationsWrite,
                    ScopedAuthorizationOptions.WithPlatformOverride,
                    ct);
                if (!access.Succeeded)
                {
                    return Results.Forbid();
                }
                if (access.AccessMode == ScopedAuthorizationAccessMode.PlatformOverride)
                {
                    return Results.Problem(title: "Forbidden", detail: OrganizationsErrors.PlatformOverrideMutationForbidden.Description, statusCode: StatusCodes.Status403Forbidden);
                }

                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<UpdateOrganizationResponse>>(
                    new UpdateOrganizationCommand(organization.Value.Id, request.Name, request.Slug),
                    ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("UpdateOrganization")
        .WithSummary("Update organization settings.")
        .Produces<UpdateOrganizationResponse>()
        .RequireAuthorization();
}
