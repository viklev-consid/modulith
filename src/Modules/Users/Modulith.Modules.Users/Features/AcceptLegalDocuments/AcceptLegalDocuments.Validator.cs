using FluentValidation;

namespace Modulith.Modules.Users.Features.AcceptLegalDocuments;

internal sealed class AcceptLegalDocumentsValidator : AbstractValidator<AcceptLegalDocumentsRequest>
{
    public AcceptLegalDocumentsValidator()
    {
        RuleFor(x => x.AcceptedDocuments).NotEmpty();
        RuleForEach(x => x.AcceptedDocuments).ChildRules(document =>
        {
            document.RuleFor(x => x.DocumentId).NotEmpty();
            document.RuleFor(x => x.Version).NotEmpty().MaximumLength(50);
            document.RuleFor(x => x.ContentHash).NotEmpty().Length(64);
        });
    }
}
