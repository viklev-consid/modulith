namespace Modulith.Modules.Users.Features.AcceptLegalDocuments;

public sealed record AcceptLegalDocumentsRequest
{
    public IReadOnlyList<AcceptedLegalDocumentRequest> AcceptedDocuments { get; init; } = [];
}

public sealed record AcceptedLegalDocumentRequest
{
    public Guid DocumentId { get; init; }
    public string Version { get; init; } = string.Empty;
    public string ContentHash { get; init; } = string.Empty;
}
