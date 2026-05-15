using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.TwoFactor.RegenerateRecoveryCodes;

internal static class RegenerateRecoveryCodesEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost(UsersRoutes.RegenerateRecoveryCodes,
            async (
                RegenerateRecoveryCodesRequest request,
                [Microsoft.AspNetCore.Mvc.FromServices] IValidator<RegenerateRecoveryCodesRequest> validator,
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

                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<RegenerateRecoveryCodesResponse>>(
                    new RegenerateRecoveryCodesCommand(userId, request.CurrentPassword, request.Code),
                    ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("RegenerateRecoveryCodes")
        .WithSummary("Regenerate recovery codes for the current user.")
        .Produces<RegenerateRecoveryCodesResponse>()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .RequireAuthorization()
        .RequireRateLimiting("auth");
}
