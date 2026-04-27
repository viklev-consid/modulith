using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Wolverine;

namespace Modulith.Modules.Users.Features.ExternalLogin.Google.Login;

internal static class GoogleLoginEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost(UsersRoutes.GoogleLogin,
            async (
                GoogleLoginRequest request,
                [Microsoft.AspNetCore.Mvc.FromServices] IValidator<GoogleLoginRequest> validator,
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
                var command = new GoogleLoginCommand(request.IdToken, ip, ua);
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<GoogleLoginResponse>>(command, ct);
                return result.ToProblemDetailsOr(r => r.IsPending ? Results.Accepted() : Results.Ok(r));
            })
        .WithName("GoogleLogin")
        .WithSummary("Authenticate with a Google ID token. Returns 200 with tokens if the account is linked, or 202 if a confirmation email was sent.")
        .Produces<GoogleLoginResponse>()
        .Produces(StatusCodes.Status202Accepted)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .AllowAnonymous()
        .RequireRateLimiting("auth");
}
