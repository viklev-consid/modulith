using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Audit.Persistence;
using Modulith.Shared.Kernel.Gdpr;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Audit.Gdpr;

public sealed class AuditPersonalDataEraser(AuditDbContext db) : IPersonalDataEraser
{
    public async Task<ErasureResult> EraseAsync(UserRef user, ErasureStrategy strategy, CancellationToken ct)
    {
        var entries = await db.AuditEntries
            .Where(e => e.ActorId == user.UserId)
            .ToListAsync(ct);

        foreach (var entry in entries)
        {
            var redacted = RedactEmailFromPayload(entry.Payload, user.DisplayName);
            entry.AnonymizeActor(redacted);
        }

        await db.SaveChangesAsync(ct);

        return new ErasureResult(user.UserId, ErasureStrategy.Anonymize, entries.Count,
            "Actor anonymized; payload email references redacted.");
    }

    private static string RedactEmailFromPayload(string payload, string? displayName)
    {
        if (string.IsNullOrEmpty(payload))
            return payload;

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var obj = doc.RootElement.EnumerateObject()
                .ToDictionary(p => p.Name, p => (object?)p.Value.ToString());

            foreach (var key in obj.Keys.Where(
                k => k.Contains("email", StringComparison.OrdinalIgnoreCase) ||
                     k.Contains("mail", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                obj[key] = "[REDACTED]";
            }

            return JsonSerializer.Serialize(obj);
        }
        catch (JsonException)
        {
            return "[REDACTED]";
        }
    }
}
