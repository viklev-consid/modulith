using FluentValidation;
using Microsoft.Extensions.Options;

namespace Modulith.Modules.Users.Features.ChangePassword;

internal sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordValidator(IOptions<UsersOptions> options)
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(options.Value.MinPasswordLength).MaximumLength(128);
    }
}
