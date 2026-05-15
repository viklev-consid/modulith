using FluentValidation;

namespace Modulith.Modules.Users.Features.TwoFactor.RegenerateRecoveryCodes;

internal sealed class RegenerateRecoveryCodesValidator : AbstractValidator<RegenerateRecoveryCodesRequest>
{
    public RegenerateRecoveryCodesValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().Matches("^[0-9]{6}$");
    }
}
