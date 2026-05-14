using FluentValidation;

namespace Modulith.Modules.Users.Features.TwoFactor.RegenerateRecoveryCodes;

internal sealed class RegenerateRecoveryCodesValidator : AbstractValidator<RegenerateRecoveryCodesRequest>
{
    public RegenerateRecoveryCodesValidator()
    {
        RuleFor(x => x.Code).NotEmpty();
    }
}
