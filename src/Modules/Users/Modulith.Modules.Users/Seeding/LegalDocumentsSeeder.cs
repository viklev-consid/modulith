using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Legal;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Infrastructure.Seeding;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Seeding;

public sealed class LegalDocumentsSeeder(
    UsersDbContext db,
    IOptions<UsersOptions> options,
    IClock clock,
    ILegalComplianceService complianceService) : IModuleSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var optionsValue = options.Value;

        var changed = false;

        changed |= await EnsureDocumentAsync(
            LegalDocumentType.TermsOfService,
            optionsValue.TermsOfServiceVersion,
            "Terms of Service",
            $"terms-of-service.v{optionsValue.TermsOfServiceVersion}.md",
            cancellationToken);

        changed |= await EnsureDocumentAsync(
            LegalDocumentType.PrivacyPolicy,
            optionsValue.PrivacyPolicyVersion,
            "Privacy Policy",
            $"privacy-policy.v{optionsValue.PrivacyPolicyVersion}.md",
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        if (changed)
        {
            await complianceService.InvalidateAllContinuedUseComplianceAsync(cancellationToken);
        }
    }

    private async Task<bool> EnsureDocumentAsync(
        LegalDocumentType documentType,
        string version,
        string title,
        string resourceFileName,
        CancellationToken ct)
    {
        var exists = await db.LegalDocuments
            .AnyAsync(d => d.DocumentType == documentType && d.Version == version, ct);

        if (exists)
        {
            return false;
        }

        var markdownContent = await ReadEmbeddedMarkdownAsync(resourceFileName, ct);
        var now = clock.UtcNow;

        db.LegalDocuments.Add(LegalDocument.Publish(
            documentType,
            version,
            title,
            markdownContent,
            ComputeSha256(markdownContent),
            effectiveAt: now,
            publishedAt: now,
            isRequiredForOnboarding: true));

        return true;
    }

    private static async Task<string> ReadEmbeddedMarkdownAsync(string fileName, CancellationToken ct)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(fileName, StringComparison.Ordinal));

        if (resourceName is null)
        {
            throw new InvalidOperationException($"Missing embedded legal document resource '{fileName}'.");
        }

        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Unable to open embedded legal document resource '{fileName}'.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync(ct);
    }

    private static string ComputeSha256(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
