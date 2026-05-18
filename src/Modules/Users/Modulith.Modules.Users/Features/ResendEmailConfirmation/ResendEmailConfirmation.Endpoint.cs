using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Wolverine;

namespace Modulith.Modules.Users.Features.ResendEmailConfirmation;

internal static class ResendEmailConfirmationEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost(UsersRoutes.ResendEmailConfirmation,
            async (ResendEmailConfirmationRequest request, [Microsoft.AspNetCore.Mvc.FromServices] IValidator<ResendEmailConfirmationRequest> validator, IMessageBus bus, CancellationToken ct) =>
            {
                var validation = await validator.ValidateAsync(request, ct);
                if (!validation.IsValid)
                {
                    return Results.Ok(new ResendEmailConfirmationResponse());
                }

                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<ResendEmailConfirmationResponse>>(
                    new ResendEmailConfirmationCommand(request.Email),
                    ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("ResendEmailConfirmation")
        .WithSummary("Request another email confirmation link.")
        .Produces<ResendEmailConfirmationResponse>()
        .AllowAnonymous()
        .RequireRateLimiting("auth");
}
