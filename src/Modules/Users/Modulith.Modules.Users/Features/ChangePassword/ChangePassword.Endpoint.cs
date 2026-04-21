using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.ChangePassword;

internal static class ChangePasswordEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost(UsersRoutes.ChangePassword,
            async (
                ChangePasswordRequest request,
                [Microsoft.AspNetCore.Mvc.FromServices] IValidator<ChangePasswordRequest> validator,
                [Microsoft.AspNetCore.Mvc.FromServices] ICurrentUser currentUser,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                var validation = await validator.ValidateAsync(request, ct);
                if (!validation.IsValid)
                    return Results.ValidationProblem(validation.ToDictionary(), statusCode: StatusCodes.Status422UnprocessableEntity);

                if (!Guid.TryParse(currentUser.Id, out var userId))
                    return Results.Unauthorized();

                var command = new ChangePasswordCommand(
                    userId,
                    request.CurrentPassword,
                    request.NewPassword,
                    currentUser.ActiveRefreshTokenId);

                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<ChangePasswordResponse>>(command, ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("ChangePassword")
        .WithSummary("Change the authenticated user's password. Revokes all other sessions.")
        .Produces<ChangePasswordResponse>()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .RequireAuthorization()
        .RequireRateLimiting("auth");
}
