using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Wolverine;

namespace Modulith.Modules.Users.Features.ConfirmEmail;

internal static class ConfirmEmailEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost(UsersRoutes.ConfirmEmail,
            async (ConfirmEmailRequest request, [Microsoft.AspNetCore.Mvc.FromServices] IValidator<ConfirmEmailRequest> validator, IMessageBus bus, CancellationToken ct) =>
            {
                var validation = await validator.ValidateAsync(request, ct);
                if (!validation.IsValid)
                {
                    return Results.ValidationProblem(validation.ToDictionary(), statusCode: StatusCodes.Status422UnprocessableEntity);
                }

                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<ConfirmEmailResponse>>(new ConfirmEmailCommand(request.Token), ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("ConfirmEmail")
        .WithSummary("Confirm a newly registered account using the token from the confirmation email.")
        .Produces<ConfirmEmailResponse>()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .AllowAnonymous()
        .RequireRateLimiting("auth");
}
