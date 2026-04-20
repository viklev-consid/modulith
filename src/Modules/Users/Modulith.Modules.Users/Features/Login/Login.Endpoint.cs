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
        app.MapPost("/v1/users/login",
            async (LoginRequest request, IValidator<LoginRequest> validator, IMessageBus bus, CancellationToken ct) =>
            {
                var validation = await validator.ValidateAsync(request, ct);
                if (!validation.IsValid)
                    return Results.ValidationProblem(validation.ToDictionary());

                var command = new LoginCommand(request.Email, request.Password);
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<LoginResponse>>(command, ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("Login")
        .WithSummary("Authenticate and receive an access token.")
        .Produces<LoginResponse>()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .AllowAnonymous();
}
