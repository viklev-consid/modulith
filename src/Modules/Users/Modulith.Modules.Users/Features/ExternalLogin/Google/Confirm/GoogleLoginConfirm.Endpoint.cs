using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Wolverine;

namespace Modulith.Modules.Users.Features.ExternalLogin.Google.Confirm;

internal static class GoogleLoginConfirmEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost(UsersRoutes.GoogleLoginConfirm,
            async (
                GoogleLoginConfirmRequest request,
                [Microsoft.AspNetCore.Mvc.FromServices] IValidator<GoogleLoginConfirmRequest> validator,
                HttpContext httpContext,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                var validation = await validator.ValidateAsync(request, ct);
                if (!validation.IsValid)
                {
                    return Results.ValidationProblem(validation.ToDictionary(), statusCode: StatusCodes.Status422UnprocessableEntity);
                }

                var ip = httpContext.Connection.RemoteIpAddress?.ToString();
                var ua = httpContext.Request.Headers.UserAgent.ToString();
                var command = new GoogleLoginConfirmCommand(request.Token, ip, ua);
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<GoogleLoginConfirmResponse>>(command, ct);
                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("GoogleLoginConfirm")
        .WithSummary("Confirm a pending Google login using the token sent by email. Returns tokens on success.")
        .Produces<GoogleLoginConfirmResponse>()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .AllowAnonymous()
        .RequireRateLimiting("auth");
}
