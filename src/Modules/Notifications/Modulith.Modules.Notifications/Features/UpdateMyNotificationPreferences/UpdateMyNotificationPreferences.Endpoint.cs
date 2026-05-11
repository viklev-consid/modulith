using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Notifications.Features.UpdateMyNotificationPreferences;

internal static class UpdateMyNotificationPreferencesEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPut(NotificationsRoutes.MyNotificationPreferences,
            async (UpdateMyNotificationPreferencesRequest request, ICurrentUser currentUser, IMessageBus bus, CancellationToken ct) =>
            {
                if (currentUser.Id is null || !Guid.TryParse(currentUser.Id, out var userId))
                {
                    return Results.Unauthorized();
                }

                var command = new UpdateMyNotificationPreferencesCommand(
                    userId,
                    request.Preferences.Select(p => new UpdateMyNotificationPreference(
                        p.Category,
                        p.BellEnabled,
                        p.EmailEnabled)).ToList());

                var result = await bus.InvokeAsync<ErrorOr<Success>>(command, ct);
                return result.ToProblemDetailsOr(_ => Results.NoContent());
            })
        .WithName("UpdateMyNotificationPreferences")
        .WithSummary("Update the authenticated user's notification preferences.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .RequireAuthorization();
}
