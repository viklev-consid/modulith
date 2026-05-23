using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Organizations.Features.CreateOrganization;

internal static class CreateOrganizationEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost(OrganizationsRoutes.Prefix,
            async (
                CreateOrganizationRequest request,
                IValidator<CreateOrganizationRequest> validator,
                ICurrentUser currentUser,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                if (currentUser.Id is null || !Guid.TryParse(currentUser.Id, out var userId))
                {
                    return Results.Unauthorized();
                }

                var validation = await validator.ValidateAsync(request, ct);
                if (!validation.IsValid)
                {
                    return Results.ValidationProblem(validation.ToDictionary(), statusCode: StatusCodes.Status422UnprocessableEntity);
                }

                var command = new CreateOrganizationCommand(request.Name, request.Slug, userId);
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<CreateOrganizationResponse>>(command, ct);
                return result.ToProblemDetailsOr(r => Results.Created($"{OrganizationsRoutes.Prefix}/{r.Slug}", r));
            })
        .WithName("CreateOrganization")
        .WithSummary("Create an organization and make the caller its owner.")
        .Produces<CreateOrganizationResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
        .RequireAuthorization();
}
