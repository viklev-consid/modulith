using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Notifications.Features.ListMyNotifications;

internal static class ListMyNotificationsEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet(NotificationsRoutes.MyNotifications,
            async (ICurrentUser currentUser, IMessageBus bus, CancellationToken ct, string? status = null, int limit = 20, DateTimeOffset? before = null, Guid? beforeId = null) =>
            {
                if (currentUser.Id is null || !Guid.TryParse(currentUser.Id, out var userId))
                {
                    return Results.Unauthorized();
                }

                var result = await bus.InvokeAsync<ErrorOr<ListMyNotificationsResponse>>(
                    new ListMyNotificationsQuery(userId, status, limit, before, beforeId),
                    ct);

                return result.ToProblemDetailsOr(Results.Ok);
            })
        .WithName("ListMyNotifications")
        .WithSummary("List the authenticated user's in-app notifications.")
        .Produces<ListMyNotificationsResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .RequireAuthorization();
}
