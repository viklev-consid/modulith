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
