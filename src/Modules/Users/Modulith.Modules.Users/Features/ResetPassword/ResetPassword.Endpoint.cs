using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Wolverine;

namespace Modulith.Modules.Users.Features.ResetPassword;

internal static class ResetPasswordEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost(UsersRoutes.ResetPassword,
            async (
                ResetPasswordRequest request,
                [Microsoft.AspNetCore.Mvc.FromServices] IValidator<ResetPasswordRequest> validator,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                var validation = await validator.ValidateAsync(request, ct);
                if (!validation.IsValid)
                    return Results.ValidationProblem(validation.ToDictionary(), statusCode: StatusCodes.Status422UnprocessableEntity);

                var command = new ResetPasswordCommand(request.Token, request.NewPassword);
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<ResetPasswordResponse>>(command, ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("ResetPassword")
        .WithSummary("Complete a password reset using the token from the reset email.")
        .Produces<ResetPasswordResponse>()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .AllowAnonymous()
        .RequireRateLimiting("auth");
}
