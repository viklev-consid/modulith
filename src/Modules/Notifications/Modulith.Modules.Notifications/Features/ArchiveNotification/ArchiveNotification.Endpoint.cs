using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Notifications.Features.ArchiveNotification;

internal static class ArchiveNotificationEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete(NotificationsRoutes.MyNotificationById,
            async (Guid notificationId, ICurrentUser currentUser, IMessageBus bus, CancellationToken ct) =>
            {
                if (currentUser.Id is null || !Guid.TryParse(currentUser.Id, out var userId))
                {
                    return Results.Unauthorized();
                }

                var result = await bus.InvokeAsync<ErrorOr<Success>>(
                    new ArchiveNotificationCommand(userId, notificationId),
                    ct);

                return result.ToProblemDetailsOr(_ => Results.NoContent());
            })
        .WithName("ArchiveNotification")
        .WithSummary("Archive one in-app notification.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization();
}
