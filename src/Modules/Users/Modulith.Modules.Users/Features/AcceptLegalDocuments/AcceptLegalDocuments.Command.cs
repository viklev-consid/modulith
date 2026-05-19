using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Features.AcceptLegalDocuments;

public sealed record AcceptLegalDocumentsCommand(
    UserId UserId,
    IReadOnlyList<AcceptedLegalDocumentCommand> AcceptedDocuments,
    string? IpAddress,
    string? UserAgent);

public sealed record AcceptedLegalDocumentCommand(Guid DocumentId, string Version, string ContentHash);
