namespace Modulith.Modules.Users.Features.ExternalLogin.CompleteOnboarding;

public sealed record CompleteOnboardingCommand(
    Guid UserId,
    bool AcceptTerms,
    bool AcceptMarketingEmails,
    string? IpAddress,
    string? UserAgent);
