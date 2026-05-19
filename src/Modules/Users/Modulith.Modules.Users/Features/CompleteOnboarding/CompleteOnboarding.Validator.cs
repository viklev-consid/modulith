using FluentValidation;

namespace Modulith.Modules.Users.Features.CompleteOnboarding;

internal sealed class CompleteOnboardingValidator : AbstractValidator<CompleteOnboardingRequest>
{
    public CompleteOnboardingValidator()
    {
        RuleFor(x => x.AcceptedDocuments)
            .NotEmpty()
            .WithMessage("Accepted legal documents are required.");

        RuleForEach(x => x.AcceptedDocuments).ChildRules(document =>
        {
            document.RuleFor(x => x.DocumentId).NotEmpty();
            document.RuleFor(x => x.Version).NotEmpty().MaximumLength(50);
            document.RuleFor(x => x.ContentHash).NotEmpty().Length(64);
        });
    }
}
