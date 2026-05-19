namespace Modulith.Modules.Users.Features.CompleteOnboarding;

public sealed record CompleteOnboardingCommand(
    Guid UserId,
    bool AcceptTerms,
    bool AcceptMarketingEmails,
    IReadOnlyList<AcceptedLegalDocumentCommand> AcceptedDocuments,
    string? IpAddress,
    string? UserAgent);

public sealed record AcceptedLegalDocumentCommand(Guid DocumentId, string Version, string ContentHash);
