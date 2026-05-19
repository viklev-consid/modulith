namespace Modulith.Modules.Users.Features.GetOnboardingLegalRequirements;

public sealed record GetOnboardingLegalRequirementsResponse(IReadOnlyList<OnboardingLegalDocumentResponse> Documents);

public sealed record OnboardingLegalDocumentResponse(
    Guid Id,
    string Type,
    string Title,
    string Version,
    DateTimeOffset EffectiveAt,
    string ContentHash,
    string Markdown);
