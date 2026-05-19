using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Legal;

public sealed record LegalComplianceResult(
    IReadOnlyList<LegalDocument> MissingDocuments,
    LegalDocumentBlockingLevel BlockingLevel)
{
    public bool IsCompliant => MissingDocuments.Count == 0;
}
