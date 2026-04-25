namespace Modulith.Modules.Users.Features.ExternalLogin.CompleteOnboarding;

public sealed record CompleteOnboardingCommand(
    Guid UserId,
    string TermsVersion,
    string PrivacyPolicyVersion,
    string? IpAddress,
    string? UserAgent);
