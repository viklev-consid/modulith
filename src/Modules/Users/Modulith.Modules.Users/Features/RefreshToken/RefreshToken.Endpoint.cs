using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Wolverine;

namespace Modulith.Modules.Users.Features.RefreshToken;

internal static class RefreshTokenEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost(UsersRoutes.RefreshToken,
            async (
                RefreshTokenRequest request,
                [Microsoft.AspNetCore.Mvc.FromServices] IValidator<RefreshTokenRequest> validator,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                var validation = await validator.ValidateAsync(request, ct);
                if (!validation.IsValid)
                {
                    return Results.ValidationProblem(validation.ToDictionary(), statusCode: StatusCodes.Status422UnprocessableEntity);
                }

                var command = new RefreshTokenCommand(request.RefreshToken);
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<RefreshTokenResponse>>(command, ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("RefreshToken")
        .WithSummary("Exchange a refresh token for a new access token and rotated refresh token.")
        .Produces<RefreshTokenResponse>()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .AllowAnonymous()
        .RequireRateLimiting("auth");
}
