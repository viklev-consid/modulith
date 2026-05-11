using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Notifications.Features.GetUnreadNotificationCount;

internal static class GetUnreadNotificationCountEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet(NotificationsRoutes.MyNotificationsUnreadCount,
            async (ICurrentUser currentUser, IMessageBus bus, CancellationToken ct) =>
            {
                if (currentUser.Id is null || !Guid.TryParse(currentUser.Id, out var userId))
                {
                    return Results.Unauthorized();
                }

                var result = await bus.InvokeAsync<ErrorOr<GetUnreadNotificationCountResponse>>(
                    new GetUnreadNotificationCountQuery(userId),
                    ct);

                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("GetUnreadNotificationCount")
        .WithSummary("Get the authenticated user's unread in-app notification count.")
        .Produces<GetUnreadNotificationCountResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .RequireAuthorization();
}
