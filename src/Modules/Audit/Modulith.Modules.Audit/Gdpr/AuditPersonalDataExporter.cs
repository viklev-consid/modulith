using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Audit.Persistence;
using Modulith.Shared.Kernel.Gdpr;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Audit.Gdpr;

public sealed class AuditPersonalDataExporter(AuditDbContext db) : IPersonalDataExporter
{
    public async Task<PersonalDataExport> ExportAsync(UserRef user, CancellationToken ct)
    {
        var entries = await db.AuditEntries
            .Where(e => e.ActorId == user.UserId || e.ResourceId == user.UserId)
            .OrderBy(e => e.OccurredAt)
            .Select(e => new { e.EventType, e.ResourceType, e.ResourceId, e.OccurredAt })
            .ToListAsync(ct);

        var data = new Dictionary<string, object?>
        {
            ["auditEntries"] = entries.Select(e => new
            {
                eventType = e.EventType,
                resourceType = e.ResourceType,
                resourceId = e.ResourceId,
                occurredAt = e.OccurredAt,
            }).ToList(),
        };

        return new PersonalDataExport(user.UserId, "Audit", data);
    }
}
