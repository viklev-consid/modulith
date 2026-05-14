using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.TwoFactor.DisableTwoFactor;

internal static class DisableTwoFactorEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete(UsersRoutes.TwoFactor,
            async (
                DisableTwoFactorRequest request,
                [Microsoft.AspNetCore.Mvc.FromServices] IValidator<DisableTwoFactorRequest> validator,
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

                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<DisableTwoFactorResponse>>(
                    new DisableTwoFactorCommand(userId, request.CurrentPassword, request.Code),
                    ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("DisableTwoFactor")
        .WithSummary("Disable two-factor authentication for the current user.")
        .Produces<DisableTwoFactorResponse>()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .RequireAuthorization()
        .RequireRateLimiting("auth");
}
