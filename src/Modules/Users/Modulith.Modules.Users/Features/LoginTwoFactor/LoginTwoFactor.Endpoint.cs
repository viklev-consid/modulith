using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Wolverine;

namespace Modulith.Modules.Users.Features.LoginTwoFactor;

internal static class LoginTwoFactorEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost(UsersRoutes.LoginTwoFactor,
            async (
                LoginTwoFactorRequest request,
                [Microsoft.AspNetCore.Mvc.FromServices] IValidator<LoginTwoFactorRequest> validator,
                HttpContext httpContext,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                var validation = await validator.ValidateAsync(request, ct);
                if (!validation.IsValid)
                {
                    return Results.ValidationProblem(validation.ToDictionary(), statusCode: StatusCodes.Status422UnprocessableEntity);
                }

                var ip = httpContext.Connection.RemoteIpAddress?.ToString();
                var command = new LoginTwoFactorCommand(request.ChallengeToken, request.Code, ip);
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<LoginTwoFactorResponse>>(command, ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("LoginTwoFactor")
        .WithSummary("Complete a two-factor login challenge and receive tokens.")
        .Produces<LoginTwoFactorResponse>()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .AllowAnonymous()
        .RequireRateLimiting("auth");
}
