using Wolverine.Persistence.Durability;
using Wolverine.Persistence.Durability.DeadLetterManagement;

namespace Modulith.Api.Infrastructure.DeadLetters;

/// <summary>
/// Minimal-API endpoints for dead-letter queue management, mounted at
/// <c>/v1/admin/dead-letters</c> and restricted to the <c>Admin</c>
/// authorization policy (requires <c>role == "admin"</c>).
/// </summary>
internal static class DeadLetterAdminEndpoints
{
    internal static IEndpointRouteBuilder MapDeadLetterAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/admin/dead-letters")
            .RequireAuthorization("Admin")
            .WithTags("Admin — Dead Letters")
            .DisableRateLimiting();

        // ── List ──────────────────────────────────────────────────────────────
        group.MapGet("/", async (
                IMessageStore store,
                int pageNumber = 1,
                int pageSize = 50,
                string? messageType = null,
                string? exceptionType = null,
                CancellationToken ct = default) =>
            {
                var query = new DeadLetterEnvelopeQuery
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    MessageType = messageType,
                    ExceptionType = exceptionType
                };
                var results = await store.DeadLetters.QueryAsync(query, ct);
                return Results.Ok(results);
            })
            .WithName("ListDeadLetters")
            .WithSummary("Page through dead-lettered messages, optionally filtered by message type or exception type.")
            .Produces<DeadLetterEnvelopeResults>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // ── Single ─────────────────────────────────────────────────────────
        group.MapGet("/{id:guid}", async (
                Guid id,
                IMessageStore store,
                CancellationToken ct) =>
            {
                var envelope = await store.DeadLetters.DeadLetterEnvelopeByIdAsync(id);
                return envelope is null ? Results.NotFound() : Results.Ok(envelope);
            })
            .WithName("GetDeadLetter")
            .WithSummary("Get a single dead-lettered message by envelope ID.")
            .Produces<DeadLetterEnvelope>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // ── Replay ─────────────────────────────────────────────────────────
        group.MapPost("/replay", async (
                DeadLetterEnvelopeQuery query,
                IMessageStore store,
                CancellationToken ct) =>
            {
                await store.DeadLetters.ReplayAsync(query, ct);
                return Results.Accepted();
            })
            .WithName("ReplayDeadLetters")
            .WithSummary("Re-enqueue dead-lettered messages matching the query for reprocessing.")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // ── Discard ────────────────────────────────────────────────────────
        group.MapPost("/discard", async (
                DeadLetterEnvelopeQuery query,
                IMessageStore store,
                CancellationToken ct) =>
            {
                await store.DeadLetters.DiscardAsync(query, ct);
                return Results.NoContent();
            })
            .WithName("DiscardDeadLetters")
            .WithSummary("Permanently delete dead-lettered messages matching the query.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }
}
