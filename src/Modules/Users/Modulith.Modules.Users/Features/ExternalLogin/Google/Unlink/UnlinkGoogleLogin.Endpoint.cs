using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.ExternalLogin.Google.Unlink;

internal static class UnlinkGoogleLoginEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete(UsersRoutes.UnlinkGoogleLogin,
            async (ICurrentUser currentUser, IMessageBus bus, CancellationToken ct) =>
            {
                if (currentUser.Id is null || !Guid.TryParse(currentUser.Id, out var userId))
                {
                    return Results.Unauthorized();
                }

                var command = new UnlinkGoogleLoginCommand(userId);
                var result = await bus.InvokeAsync<ErrorOr<Success>>(command, ct);
                return result.ToProblemDetailsOr(_ => Results.NoContent());
            })
        .WithName("UnlinkGoogleLogin")
        .WithSummary("Unlink the Google account from the authenticated user.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .RequireAuthorization()
        .RequireRateLimiting("write");
}
