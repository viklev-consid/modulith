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
    private static readonly TimeSpan heartbeatInterval = TimeSpan.FromSeconds(25);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet(NotificationsRoutes.MyNotificationsStream,
            async (string? clientId, ICurrentUser currentUser, IMessageBus bus, HttpContext http, CancellationToken ct) =>
            {
                if (currentUser.Id is null || !Guid.TryParse(currentUser.Id, out var userId))
                {
                    return Results.Unauthorized();
                }

                if (!IsValidClientId(clientId))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                        (StringComparer.Ordinal)
                    {
                        ["clientId"] = ["A clientId query parameter is required and must be 1-128 URL-safe characters without leading or trailing dots."],
                    });
                }

                var result = await bus.InvokeAsync<ErrorOr<StreamMyNotificationsResponse>>(
                    new StreamMyNotificationsQuery(userId, clientId!),
                    ct);

                if (result.IsError)
                {
                    return result.ToProblemDetailsOr(_ => Results.Empty);
                }

                http.Response.Headers.CacheControl = "no-cache";
                http.Response.Headers.Connection = "keep-alive";
                http.Response.ContentType = "text/event-stream";

                using var subscription = result.Value.Subscription;
                using var heartbeat = new PeriodicTimer(heartbeatInterval);
                var readTask = result.Value.Reader.WaitToReadAsync(ct).AsTask();
                var heartbeatTask = WaitForHeartbeatAsync(heartbeat, ct);

                while (!ct.IsCancellationRequested)
                {
                    var completed = await Task.WhenAny(readTask, heartbeatTask);

                    if (completed == heartbeatTask)
                    {
                        if (!await heartbeatTask)
                        {
                            break;
                        }

                        await http.Response.WriteAsync(": ping\n\n", Encoding.UTF8, ct);
                        await http.Response.Body.FlushAsync(ct);
                        heartbeatTask = WaitForHeartbeatAsync(heartbeat, ct);
                        continue;
                    }

                    if (!await readTask)
                    {
                        break;
                    }

                    while (result.Value.Reader.TryRead(out var streamEvent))
                    {
                        await http.Response.WriteAsync($"event: {streamEvent.EventName}\n", Encoding.UTF8, ct);
                        await http.Response.WriteAsync($"data: {streamEvent.Payload}\n\n", Encoding.UTF8, ct);
                        await http.Response.Body.FlushAsync(ct);
                    }

                    readTask = result.Value.Reader.WaitToReadAsync(ct).AsTask();
                }

                return Results.Empty;
            })
        .WithName("StreamMyNotifications")
        .WithSummary("Stream live in-app notification updates for the authenticated user.")
        .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .RequireAuthorization()
        .RequireRateLimiting("read");

    private static bool IsValidClientId(string? clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId) || clientId.Length > 128)
        {
            return false;
        }

        if (clientId[0] == '.' || clientId[^1] == '.')
        {
            return false;
        }

        return clientId.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '-' or '_' or '.' or '~');
    }

    private static async Task<bool> WaitForHeartbeatAsync(PeriodicTimer heartbeat, CancellationToken ct) =>
        await heartbeat.WaitForNextTickAsync(ct);
}
