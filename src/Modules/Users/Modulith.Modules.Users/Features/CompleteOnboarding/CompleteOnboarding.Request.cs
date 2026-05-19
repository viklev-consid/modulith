namespace Modulith.Modules.Users.Features.CompleteOnboarding;

public sealed record CompleteOnboardingRequest
{
    public bool? AcceptTerms { get; init; }
    public bool AcceptMarketingEmails { get; init; }
    public IReadOnlyList<AcceptedLegalDocumentRequest>? AcceptedDocuments { get; init; }
}

public sealed record AcceptedLegalDocumentRequest
{
    public Guid DocumentId { get; init; }
    public string Version { get; init; } = string.Empty;
    public string ContentHash { get; init; } = string.Empty;
}
