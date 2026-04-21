using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Wolverine;

namespace Modulith.Modules.Users.Features.Logout;

internal static class LogoutEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost(UsersRoutes.Logout,
            async (
                LogoutRequest request,
                [Microsoft.AspNetCore.Mvc.FromServices] IValidator<LogoutRequest> validator,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                var validation = await validator.ValidateAsync(request, ct);
                if (!validation.IsValid)
                    return Results.ValidationProblem(validation.ToDictionary(), statusCode: StatusCodes.Status422UnprocessableEntity);

                var command = new LogoutCommand(request.RefreshToken);
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<LogoutResponse>>(command, ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("Logout")
        .WithSummary("Revoke the provided refresh token (single-session logout).")
        .Produces<LogoutResponse>()
        .ProducesValidationProblem()
        .RequireAuthorization()
        .RequireRateLimiting("write");
}
