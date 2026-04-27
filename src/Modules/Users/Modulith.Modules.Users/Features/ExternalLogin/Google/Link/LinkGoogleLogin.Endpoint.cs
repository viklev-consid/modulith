using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ErrorOr;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.ExternalLogin.Google.Link;

internal static class LinkGoogleLoginEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost(UsersRoutes.LinkGoogleLogin,
            async (
                LinkGoogleLoginRequest request,
                [Microsoft.AspNetCore.Mvc.FromServices] IValidator<LinkGoogleLoginRequest> validator,
                ICurrentUser currentUser,
                HttpContext httpContext,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                if (currentUser.Id is null || !Guid.TryParse(currentUser.Id, out var userId))
                {
                    return Results.Unauthorized();
                }

                var validation = await validator.ValidateAsync(request, ct);
                if (!validation.IsValid)
                {
                    return Results.ValidationProblem(validation.ToDictionary(), statusCode: StatusCodes.Status422UnprocessableEntity);
                }

                var ip = httpContext.Connection.RemoteIpAddress?.ToString();
                var command = new LinkGoogleLoginCommand(userId, request.IdToken, ip);
                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<Success>>(command, ct);
                return result.ToProblemDetailsOr(_ => Results.NoContent());
            })
        .WithName("LinkGoogleLogin")
        .WithSummary("Link a Google account to the authenticated user.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .RequireAuthorization()
        .RequireRateLimiting("write");
}
