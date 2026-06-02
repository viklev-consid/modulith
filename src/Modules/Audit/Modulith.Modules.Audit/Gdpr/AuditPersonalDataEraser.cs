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
            .Where(e => e.ActorId == user.UserId || e.ResourceId == user.UserId)
            .ToListAsync(ct);

        foreach (var entry in entries)
        {
            var redacted = RedactPersonalDataFromPayload(entry.Payload, user.UserId);
            entry.Anonymize(user.UserId, redacted);
        }

        await db.SaveChangesAsync(ct);

        return new ErasureResult(user.UserId, ErasureStrategy.Anonymize, entries.Count,
            "Actor and resource references anonymized; payload personal data redacted.");
    }

    private static string RedactPersonalDataFromPayload(string payload, Guid userId)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return payload;
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            return JsonSerializer.Serialize(RedactElement(doc.RootElement, userId));
        }
        catch (JsonException)
        {
            return "[REDACTED]";
        }
    }

    private static object? RedactElement(JsonElement element, Guid userId, string? propertyName = null)
        => element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => RedactElement(p.Value, userId, p.Name), StringComparer.Ordinal),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(item => RedactElement(item, userId))
                .ToList(),
            JsonValueKind.String when IsPersonalDataProperty(propertyName) => "[REDACTED]",
            JsonValueKind.String when string.Equals(element.GetString(), userId.ToString(), StringComparison.Ordinal) => "[REDACTED]",
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var value) => value,
            JsonValueKind.Number => element.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString(),
        };

    private static bool IsPersonalDataProperty(string? propertyName)
        => propertyName is not null &&
           (propertyName.Contains("email", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("mail", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("ipAddress", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("displayName", StringComparison.OrdinalIgnoreCase));
}
