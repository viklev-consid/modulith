using FluentValidation;

namespace Modulith.Modules.Users.Features.ResetPassword;

internal sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(10);
    }
}
