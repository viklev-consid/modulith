using FluentValidation;

namespace Modulith.Modules.Users.Features.LoginTwoFactor;

internal sealed class LoginTwoFactorValidator : AbstractValidator<LoginTwoFactorRequest>
{
    public LoginTwoFactorValidator()
    {
        RuleFor(x => x.ChallengeToken).NotEmpty();
        RuleFor(x => x.Code).NotEmpty();
    }
}
