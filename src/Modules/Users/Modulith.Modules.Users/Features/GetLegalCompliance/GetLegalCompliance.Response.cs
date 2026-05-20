namespace Modulith.Modules.Users.Features.GetLegalCompliance;

public sealed record GetLegalComplianceResponse(
    bool IsCompliant,
    string BlockingLevel,
    IReadOnlyList<LegalComplianceDocumentResponse> MissingDocuments,
    IReadOnlyList<AcceptedLegalDocumentResponse> AcceptedDocuments);

public sealed record LegalComplianceDocumentResponse(
    Guid Id,
    string Type,
    string Title,
    string Version,
    DateTimeOffset EffectiveAt,
    string ContentHash,
    string Markdown);

public sealed record AcceptedLegalDocumentResponse(
    string Type,
    string Version,
    DateTimeOffset AcceptedAt,
    string? ContentHash);
