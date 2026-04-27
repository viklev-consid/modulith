namespace Modulith.Modules.Users.Features.ExternalLogin.CompleteOnboarding;

public sealed record CompleteOnboardingCommand(
    Guid UserId,
    bool AcceptMarketingEmails,
    string? IpAddress,
    string? UserAgent);
