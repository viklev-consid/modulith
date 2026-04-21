using System.Text.Json;
using Modulith.Modules.Audit.Domain;
using Modulith.Modules.Audit.Persistence;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Audit.Integration.Subscribers;

public sealed class OnPasswordResetHandler(AuditDbContext db, IClock clock)
{
    public async Task Handle(PasswordResetV1 @event, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new { @event.UserId, @event.Email });
        var entry = AuditEntry.Create(
            eventType: "user.password_reset",
            actorId: @event.UserId,
            resourceType: "User",
            resourceId: @event.UserId,
            payload: payload,
            occurredAt: clock.UtcNow);

        db.AuditEntries.Add(entry);
        await db.SaveChangesAsync(ct);
    }
}
