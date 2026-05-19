using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Legal;

public sealed class LegalComplianceService(UsersDbContext db, IClock clock) : ILegalComplianceService
{
    public async Task<LegalComplianceResult> GetContinuedUseComplianceAsync(UserId userId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var requiredDocuments = await db.LegalDocuments
            .AsNoTracking()
            .Where(d => d.SupersededAt == null)
            .Where(d => d.IsRequiredForContinuedUse)
            .Where(d => d.ContinuedUseRequiredAt == null || d.ContinuedUseRequiredAt <= now)
            .ToListAsync(ct);

        requiredDocuments = requiredDocuments
            .OrderByDescending(d => d.BlockingLevel)
            .ThenBy(d => d.DocumentType)
            .ToList();

        if (requiredDocuments.Count == 0)
        {
            return new LegalComplianceResult([], LegalDocumentBlockingLevel.None);
        }

        var acceptances = await db.TermsAcceptances
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .Select(a => new
            {
                LegalDocumentId = a.LegalDocumentId == null ? (Guid?)null : a.LegalDocumentId.Value,
                a.Version,
                a.ContentHash,
            })
            .ToListAsync(ct);

        var missingDocuments = requiredDocuments
            .Where(document => !acceptances.Any(acceptance => IsAccepted(document, acceptance.LegalDocumentId, acceptance.Version, acceptance.ContentHash)))
            .ToList();

        var blockingLevel = missingDocuments.Count == 0
            ? LegalDocumentBlockingLevel.None
            : missingDocuments.Max(d => d.BlockingLevel);

        return new LegalComplianceResult(missingDocuments, blockingLevel);
    }

    private static bool IsAccepted(LegalDocument document, Guid? legalDocumentId, string version, string? contentHash)
    {
        if (legalDocumentId == document.Id.Value)
        {
            return true;
        }

        var versionKey = $"{LegalDocumentKeys.GetPrefix(document.DocumentType)}:{document.Version}";
        return string.Equals(version, versionKey, StringComparison.Ordinal) &&
            string.Equals(contentHash, document.ContentHash, StringComparison.Ordinal);
    }
}
