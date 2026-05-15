using FluentValidation;

namespace Modulith.Modules.Users.Features.TwoFactor.ConfirmTotp;

internal sealed class ConfirmTotpValidator : AbstractValidator<ConfirmTotpRequest>
{
    public ConfirmTotpValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Matches("^[0-9]{6}$");
    }
}
