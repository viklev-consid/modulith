using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Wolverine;

namespace Modulith.Modules.Users.Features.ForgotPassword;

internal static class ForgotPasswordEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost(UsersRoutes.ForgotPassword,
            async (
                ForgotPasswordRequest request,
                [Microsoft.AspNetCore.Mvc.FromServices] IValidator<ForgotPasswordRequest> validator,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                var validation = await validator.ValidateAsync(request, ct);
                if (!validation.IsValid)
                    // Return 200 even on bad input — anti-enumeration; no useful information to return
                    return Results.Ok(new ForgotPasswordResponse());

                var command = new ForgotPasswordCommand(request.Email);
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<ForgotPasswordResponse>>(command, ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("ForgotPassword")
        .WithSummary("Request a password reset email. Always returns 200 regardless of whether the email exists.")
        .Produces<ForgotPasswordResponse>()
        .AllowAnonymous()
        .RequireRateLimiting("auth");
}
