using System.Text.Json;
using Modulith.Modules.Audit.Domain;
using Modulith.Modules.Audit.Persistence;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Audit.Integration.Subscribers;

public sealed class OnExternalLoginUnlinkedHandler(AuditDbContext db, IClock clock)
{
    public async Task Handle(ExternalLoginUnlinkedV1 @event, CancellationToken ct)
    {
        using var activity = AuditTelemetry.ActivitySource.StartActivity(nameof(OnExternalLoginUnlinkedHandler));
        AuditTelemetry.EventsProcessed.Add(1, new KeyValuePair<string, object?>("event", nameof(ExternalLoginUnlinkedV1)));

        var payload = JsonSerializer.Serialize(new { @event.UserId, @event.Provider });
        var entry = AuditEntry.Create(
            eventType: "user.external_login.unlinked",
            actorId: @event.UserId,
            resourceType: "User",
            resourceId: @event.UserId,
            payload: payload,
            occurredAt: clock.UtcNow);

        db.AuditEntries.Add(entry);
        await db.SaveChangesAsync(ct);
    }
}
