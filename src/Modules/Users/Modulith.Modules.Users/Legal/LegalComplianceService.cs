using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Legal;

public sealed partial class LegalComplianceService(
    UsersDbContext db,
    IClock clock,
    HybridCache cache,
    ILogger<LegalComplianceService> logger) : ILegalComplianceService
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(1),
    };

    public async Task<LegalComplianceResult> GetContinuedUseComplianceAsync(UserId userId, CancellationToken ct)
        => await cache.GetOrCreateAsync(
            GetCacheKey(userId),
            async token => await GetContinuedUseComplianceCoreAsync(userId, token),
            CacheOptions,
            tags: ["users:legal-compliance"],
            cancellationToken: ct);

    public async Task InvalidateContinuedUseComplianceAsync(UserId userId, CancellationToken ct) =>
        await TryInvalidateAsync(() => cache.RemoveAsync(GetCacheKey(userId), ct).AsTask());

    public async Task InvalidateAllContinuedUseComplianceAsync(CancellationToken ct) =>
        await TryInvalidateAsync(() => cache.RemoveByTagAsync("users:legal-compliance", ct).AsTask());

    private async Task<LegalComplianceResult> GetContinuedUseComplianceCoreAsync(UserId userId, CancellationToken ct)
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
            .Select(ToComplianceDocument)
            .ToList();

        var blockingLevel = missingDocuments.Count == 0
            ? LegalDocumentBlockingLevel.None
            : missingDocuments.Max(d => d.BlockingLevel);

        return new LegalComplianceResult(missingDocuments, blockingLevel);
    }

    private static string GetCacheKey(UserId userId) => $"users:legal-compliance:{userId.Value}";

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

    private static LegalComplianceDocument ToComplianceDocument(LegalDocument document) =>
        new(
            document.Id.Value,
            document.DocumentType,
            document.Title,
            document.Version,
            document.EffectiveAt,
            document.ContentHash,
            document.MarkdownContent,
            document.BlockingLevel);

    private async Task TryInvalidateAsync(Func<Task> invalidate)
    {
        try
        {
            await invalidate();
        }
        catch (InvalidOperationException ex)
        {
            LogCacheInvalidationSkipped(logger, ex);
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Legal compliance cache invalidation was skipped because the cache backend is unavailable.")]
    private static partial void LogCacheInvalidationSkipped(ILogger logger, Exception exception);
}
