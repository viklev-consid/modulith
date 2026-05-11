using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Notifications.Features.MarkAllNotificationsAsRead;

internal static class MarkAllNotificationsAsReadEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPatch(NotificationsRoutes.MyNotificationsReadAll,
            async (ICurrentUser currentUser, IMessageBus bus, CancellationToken ct) =>
            {
                if (currentUser.Id is null || !Guid.TryParse(currentUser.Id, out var userId))
                {
                    return Results.Unauthorized();
                }

                var result = await bus.InvokeAsync<ErrorOr<Success>>(
                    new MarkAllNotificationsAsReadCommand(userId),
                    ct);

                return result.ToProblemDetailsOr(_ => Results.NoContent());
            })
        .WithName("MarkAllNotificationsAsRead")
        .WithSummary("Mark all visible in-app notifications as read.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .RequireAuthorization();
}
