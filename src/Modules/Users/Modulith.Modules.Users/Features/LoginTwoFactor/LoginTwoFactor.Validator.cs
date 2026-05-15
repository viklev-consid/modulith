using FluentValidation;

namespace Modulith.Modules.Users.Features.LoginTwoFactor;

internal sealed class LoginTwoFactorValidator : AbstractValidator<LoginTwoFactorRequest>
{
    public LoginTwoFactorValidator()
    {
        RuleFor(x => x.ChallengeToken).NotEmpty();
        RuleFor(x => x.Code)
            .NotEmpty()
            .Must(code => IsTotpCode(code) || IsRecoveryCode(code))
            .WithMessage("Code must be a 6-digit authenticator code or a valid recovery code.");
    }

    private static bool IsTotpCode(string code) =>
        code.Length == 6 && code.All(char.IsDigit);

    private static bool IsRecoveryCode(string code)
    {
        var parts = code.Split('-', StringSplitOptions.None);
        return parts.Length == 4
            && parts.All(p => p.Length == 5 && p.All(Uri.IsHexDigit));
    }
}
