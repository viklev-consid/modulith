using FluentValidation;

namespace Modulith.Modules.Users.Features.ConfirmEmailChange;

internal sealed class ConfirmEmailChangeValidator : AbstractValidator<ConfirmEmailChangeRequest>
{
    public ConfirmEmailChangeValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
    }
}
