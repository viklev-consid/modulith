using FluentValidation;

namespace Modulith.Modules.Users.Features.CompleteOnboarding;

internal sealed class CompleteOnboardingValidator : AbstractValidator<CompleteOnboardingRequest>
{
    public CompleteOnboardingValidator()
    {
        RuleFor(x => x.AcceptTerms).Equal(true).WithMessage("Terms of service must be accepted.");
    }
}
