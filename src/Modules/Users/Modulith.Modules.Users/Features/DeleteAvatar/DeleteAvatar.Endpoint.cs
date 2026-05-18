using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.DeleteAvatar;

internal static class DeleteAvatarEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete(UsersRoutes.MyAvatar,
            async (
                ICurrentUser currentUser,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                if (!Guid.TryParse(currentUser.Id, out var userId))
                {
                    return Results.Unauthorized();
                }

                var result = await bus.InvokeAsync<ErrorOr.ErrorOr<ErrorOr.Deleted>>(new DeleteAvatarCommand(userId), ct);
                return result.ToProblemDetailsOr(_ => Results.NoContent());
            })
        .WithName("DeleteAvatar")
        .WithSummary("Remove the authenticated user's avatar.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .RequireRateLimiting("write");
}
