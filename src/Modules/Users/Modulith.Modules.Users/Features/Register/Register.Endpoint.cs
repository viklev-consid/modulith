using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Wolverine;

namespace Modulith.Modules.Users.Features.Register;

internal static class RegisterEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost(UsersRoutes.Register,
            async (RegisterRequest request, [Microsoft.AspNetCore.Mvc.FromServices] IValidator<RegisterRequest> validator, IMessageBus bus, CancellationToken ct) =>
            {
                var validation = await validator.ValidateAsync(request, ct);
                if (!validation.IsValid)
                {
                    return Results.ValidationProblem(validation.ToDictionary(), statusCode: StatusCodes.Status422UnprocessableEntity);
                }

                var command = new RegisterCommand(request.Email, request.Password, request.DisplayName);
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<RegisterResponse>>(command, ct);
                return result.ToProblemDetailsOr(r => Results.Created($"/v1/users/{r.UserId}", r));
            })
        .WithName("Register")
        .WithSummary("Register a new user account.")
        .Produces<RegisterResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status409Conflict)
        .AllowAnonymous();
}
