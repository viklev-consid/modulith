using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Audit.Domain;
using Modulith.Modules.Audit.Persistence;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Shared.Kernel.Interfaces;
using Modulith.Shared.Infrastructure.Persistence;
using Wolverine.Attributes;

namespace Modulith.Modules.Audit.Integration.Subscribers;

[NonTransactional]
public sealed class OnUserRegisteredHandler(AuditDbContext db, IClock clock)
{
    public async Task Handle(UserRegisteredV1 @event, CancellationToken ct)
    {
        using var activity = AuditTelemetry.ActivitySource.StartActivity(nameof(OnUserRegisteredHandler));
        AuditTelemetry.EventsProcessed.Add(1, new KeyValuePair<string, object?>("event", nameof(UserRegisteredV1)));

        var payload = JsonSerializer.Serialize(new { @event.UserId });
        var entry = AuditEntry.Create(
            eventType: "user.registered",
            actorId: @event.UserId,
            resourceType: "User",
            resourceId: @event.UserId,
            payload: payload,
            occurredAt: clock.UtcNow,
            idempotencyKey: @event.EventId);

        db.AuditEntries.Add(entry);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            // Idempotency: duplicate delivery — audit entry already recorded, nothing to do.
        }
    }
}
