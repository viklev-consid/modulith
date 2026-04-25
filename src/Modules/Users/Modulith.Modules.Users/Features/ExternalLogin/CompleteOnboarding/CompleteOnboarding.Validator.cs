using FluentValidation;

namespace Modulith.Modules.Users.Features.ExternalLogin.CompleteOnboarding;

internal sealed class CompleteOnboardingValidator : AbstractValidator<CompleteOnboardingRequest>
{
    public CompleteOnboardingValidator()
    {
        RuleFor(x => x.TermsVersion).NotEmpty();
        RuleFor(x => x.PrivacyPolicyVersion).NotEmpty();
    }
}
