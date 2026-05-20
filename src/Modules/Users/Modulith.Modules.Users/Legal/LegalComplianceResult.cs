using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Legal;

public sealed record LegalComplianceResult(
    IReadOnlyList<LegalComplianceDocument> MissingDocuments,
    LegalDocumentBlockingLevel BlockingLevel)
{
    public bool IsCompliant => MissingDocuments.Count == 0;
}

public sealed record LegalComplianceDocument(
    Guid Id,
    LegalDocumentType DocumentType,
    string Title,
    string Version,
    DateTimeOffset EffectiveAt,
    string ContentHash,
    string MarkdownContent,
    LegalDocumentBlockingLevel BlockingLevel);
