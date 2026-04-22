using System.Text.Json;
using Modulith.Modules.Audit.Domain;
using Modulith.Modules.Audit.Persistence;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Audit.Integration.Subscribers;

public sealed class OnUserRoleChangedHandler(AuditDbContext db, IClock clock)
{
    public async Task Handle(UserRoleChangedV1 @event, CancellationToken ct)
    {
        using var activity = AuditTelemetry.ActivitySource.StartActivity(nameof(OnUserRoleChangedHandler));
        AuditTelemetry.EventsProcessed.Add(1, new KeyValuePair<string, object?>("event", nameof(UserRoleChangedV1)));

        var payload = JsonSerializer.Serialize(new
        {
            @event.UserId,
            @event.OldRole,
            @event.NewRole,
            @event.ChangedBy,
        });

        var entry = AuditEntry.Create(
            eventType: "user.role_changed",
            actorId: @event.ChangedBy,
            resourceType: "User",
            resourceId: @event.UserId,
            payload: payload,
            occurredAt: clock.UtcNow);

        db.AuditEntries.Add(entry);
        await db.SaveChangesAsync(ct);
    }
}
