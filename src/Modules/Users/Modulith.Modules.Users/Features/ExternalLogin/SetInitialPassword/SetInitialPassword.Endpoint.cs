using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.ExternalLogin.SetInitialPassword;

internal static class SetInitialPasswordEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost(UsersRoutes.SetInitialPassword,
            async (
                SetInitialPasswordRequest request,
                [Microsoft.AspNetCore.Mvc.FromServices] IValidator<SetInitialPasswordRequest> validator,
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

                var command = new SetInitialPasswordCommand(userId, request.Password, request.GoogleIdToken);
                var result = await bus.InvokeAsync<ErrorOr<Success>>(command, ct);
                return result.ToProblemDetailsOr(_ => Results.NoContent());
            })
        .WithName("SetInitialPassword")
        .WithSummary("Set a password for an external-only account that has none yet.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .RequireAuthorization()
        .RequireRateLimiting("auth");
}
