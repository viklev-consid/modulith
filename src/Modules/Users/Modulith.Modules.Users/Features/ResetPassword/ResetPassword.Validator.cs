using FluentValidation;
using Microsoft.Extensions.Options;

namespace Modulith.Modules.Users.Features.ResetPassword;

internal sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordValidator(IOptions<UsersOptions> options)
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(options.Value.MinPasswordLength).MaximumLength(128);
    }
}
