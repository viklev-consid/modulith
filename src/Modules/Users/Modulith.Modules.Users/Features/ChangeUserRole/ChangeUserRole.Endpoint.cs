using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Modules.Users.Contracts.Authorization;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.ChangeUserRole;

internal static class ChangeUserRoleEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPut(UsersRoutes.ChangeRole,
            async (
                Guid userId,
                ChangeUserRoleRequest request,
                [Microsoft.AspNetCore.Mvc.FromServices] IValidator<ChangeUserRoleRequest> validator,
                ICurrentUser currentUser,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                var validation = await validator.ValidateAsync(request, ct);
                if (!validation.IsValid)
                {
                    return Results.ValidationProblem(
                        validation.ToDictionary(),
                        statusCode: StatusCodes.Status422UnprocessableEntity);
                }

                if (!Guid.TryParse(currentUser.Id, out var callerId))
                {
                    return Results.Unauthorized();
                }

                var command = new ChangeUserRoleCommand(userId, request.Role, callerId);
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<ChangeUserRoleResponse>>(command, ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("ChangeUserRole")
        .WithSummary("Change the role of a user. Admin only.")
        .Produces<ChangeUserRoleResponse>()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization(UsersPermissions.RolesWrite)
        .RequireRateLimiting("write");
}
