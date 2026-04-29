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

[NonTransactional]
public sealed class OnUserRoleChangedHandler(AuditDbContext db, IClock clock)
{
    public async Task Handle(UserRoleChangedV1 @event, Envelope envelope, CancellationToken ct)
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
