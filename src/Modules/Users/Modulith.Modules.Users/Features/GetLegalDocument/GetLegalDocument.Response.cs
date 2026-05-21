namespace Modulith.Modules.Users.Features.GetLegalDocument;

public sealed record GetLegalDocumentResponse(
    Guid Id,
    string Type,
    string Title,
    string Version,
    DateTimeOffset EffectiveAt,
    DateTimeOffset PublishedAt,
    string ContentHash,
    string Markdown);
