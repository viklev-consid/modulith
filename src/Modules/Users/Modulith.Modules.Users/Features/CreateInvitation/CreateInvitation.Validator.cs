using FluentValidation;

namespace Modulith.Modules.Users.Features.CreateInvitation;

internal sealed class CreateInvitationValidator : AbstractValidator<CreateInvitationRequest>
{
    public CreateInvitationValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(254)
            .EmailAddress();
    }
}
