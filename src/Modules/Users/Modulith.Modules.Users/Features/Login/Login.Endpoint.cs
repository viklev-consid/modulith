using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Wolverine;

namespace Modulith.Modules.Users.Features.Login;

internal static class LoginEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost(UsersRoutes.Login,
            async (
                LoginRequest request,
                [Microsoft.AspNetCore.Mvc.FromServices] IValidator<LoginRequest> validator,
                HttpContext httpContext,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                var validation = await validator.ValidateAsync(request, ct);
                if (!validation.IsValid)
                    return Results.ValidationProblem(validation.ToDictionary(), statusCode: StatusCodes.Status422UnprocessableEntity);

                var ip = httpContext.Connection.RemoteIpAddress?.ToString();
                var command = new LoginCommand(request.Email, request.Password, ip);
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<LoginResponse>>(command, ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("Login")
        .WithSummary("Authenticate and receive an access token and a refresh token.")
        .Produces<LoginResponse>()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .AllowAnonymous();
}
