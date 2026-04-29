using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Audit.Domain;
using Modulith.Modules.Audit.Persistence;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Shared.Kernel.Interfaces;
using Npgsql;
using Wolverine;
using Wolverine.Attributes;

namespace Modulith.Modules.Audit.Integration.Subscribers;

/// <summary>Audit handler for confirmed email changes (Phase 9.5 email-change flow).</summary>
[NonTransactional]
public sealed class OnEmailChangedHandler(AuditDbContext db, IClock clock)
{
    public async Task Handle(EmailChangedV1 @event, Envelope envelope, CancellationToken ct)
    {
        using var activity = AuditTelemetry.ActivitySource.StartActivity(nameof(OnEmailChangedHandler));
        AuditTelemetry.EventsProcessed.Add(1, new KeyValuePair<string, object?>("event", nameof(EmailChangedV1)));

        var payload = JsonSerializer.Serialize(new { @event.UserId });
        var entry = AuditEntry.Create(
            eventType: "user.email_changed",
            actorId: @event.UserId,
            resourceType: "User",
            resourceId: @event.UserId,
            payload: payload,
            occurredAt: clock.UtcNow,
            idempotencyKey: envelope.Id);

        db.AuditEntries.Add(entry);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // Idempotency: duplicate delivery — audit entry already recorded, nothing to do.
        }
    }
}
