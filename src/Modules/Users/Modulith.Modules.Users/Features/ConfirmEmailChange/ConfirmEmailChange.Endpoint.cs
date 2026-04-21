using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.ConfirmEmailChange;

internal static class ConfirmEmailChangeEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost(UsersRoutes.ConfirmEmailChange,
            async (
                ConfirmEmailChangeRequest request,
                [Microsoft.AspNetCore.Mvc.FromServices] IValidator<ConfirmEmailChangeRequest> validator,
                [Microsoft.AspNetCore.Mvc.FromServices] ICurrentUser currentUser,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                var validation = await validator.ValidateAsync(request, ct);
                if (!validation.IsValid)
                {
                    return Results.ValidationProblem(validation.ToDictionary(), statusCode: StatusCodes.Status422UnprocessableEntity);
                }

                if (!Guid.TryParse(currentUser.Id, out var userId))
                {
                    return Results.Unauthorized();
                }

                var command = new ConfirmEmailChangeCommand(userId, request.Token);
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<ConfirmEmailChangeResponse>>(command, ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("ConfirmEmailChange")
        .WithSummary("Confirm an email address change using the token from the confirmation email.")
        .Produces<ConfirmEmailChangeResponse>()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .RequireAuthorization()
        .RequireRateLimiting("auth");
}
