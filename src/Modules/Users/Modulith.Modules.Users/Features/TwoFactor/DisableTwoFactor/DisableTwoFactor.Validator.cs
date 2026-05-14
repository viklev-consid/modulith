using FluentValidation;

namespace Modulith.Modules.Users.Features.TwoFactor.DisableTwoFactor;

internal sealed class DisableTwoFactorValidator : AbstractValidator<DisableTwoFactorRequest>
{
    public DisableTwoFactorValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.Code).NotEmpty();
    }
}
