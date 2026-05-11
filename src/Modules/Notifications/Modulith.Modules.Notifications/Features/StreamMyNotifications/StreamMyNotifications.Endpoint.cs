using System.Text;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Http;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Notifications.Features.StreamMyNotifications;

internal static class StreamMyNotificationsEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet(NotificationsRoutes.MyNotificationsStream,
            async (ICurrentUser currentUser, IMessageBus bus, HttpContext http, CancellationToken ct) =>
            {
                if (currentUser.Id is null || !Guid.TryParse(currentUser.Id, out var userId))
                {
                    return Results.Unauthorized();
                }

                var result = await bus.InvokeAsync<ErrorOr<StreamMyNotificationsResponse>>(
                    new StreamMyNotificationsQuery(userId),
                    ct);

                if (result.IsError)
                {
                    return result.ToProblemDetailsOr(_ => Results.Empty);
                }

                http.Response.Headers.CacheControl = "no-cache";
                http.Response.Headers.Connection = "keep-alive";
                http.Response.ContentType = "text/event-stream";

                using var subscription = result.Value.Subscription;
                await foreach (var streamEvent in result.Value.Reader.ReadAllAsync(ct))
                {
                    await http.Response.WriteAsync($"event: {streamEvent.EventName}\n", Encoding.UTF8, ct);
                    await http.Response.WriteAsync($"data: {streamEvent.Payload}\n\n", Encoding.UTF8, ct);
                    await http.Response.Body.FlushAsync(ct);
                }

                return Results.Empty;
            })
        .WithName("StreamMyNotifications")
        .WithSummary("Stream live in-app notification updates for the authenticated user.")
        .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .RequireAuthorization();
}
