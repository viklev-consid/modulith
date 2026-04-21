using System.Text.Json;
using Modulith.Modules.Audit.Domain;
using Modulith.Modules.Audit.Persistence;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Audit.Integration.Subscribers;

/// <summary>Audit handler for confirmed email changes (Phase 9.5 email-change flow).</summary>
public sealed class OnEmailChangedHandler(AuditDbContext db, IClock clock)
{
    public async Task Handle(EmailChangedV1 @event, CancellationToken ct)
    {
        using var activity = AuditTelemetry.ActivitySource.StartActivity(nameof(OnEmailChangedHandler));
        AuditTelemetry.EventsProcessed.Add(1, new KeyValuePair<string, object?>("event", nameof(EmailChangedV1)));

        var payload = JsonSerializer.Serialize(new { @event.UserId, @event.OldEmail, @event.NewEmail });
        var entry = AuditEntry.Create(
            eventType: "user.email_changed",
            actorId: @event.UserId,
            resourceType: "User",
            resourceId: @event.UserId,
            payload: payload,
            occurredAt: clock.UtcNow);

        db.AuditEntries.Add(entry);
        await db.SaveChangesAsync(ct);
    }
}
