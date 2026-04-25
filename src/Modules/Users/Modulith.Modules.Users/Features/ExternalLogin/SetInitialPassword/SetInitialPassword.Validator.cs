using FluentValidation;
using Microsoft.Extensions.Options;

namespace Modulith.Modules.Users.Features.ExternalLogin.SetInitialPassword;

internal sealed class SetInitialPasswordValidator : AbstractValidator<SetInitialPasswordRequest>
{
    public SetInitialPasswordValidator(IOptions<UsersOptions> options)
    {
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(options.Value.MinPasswordLength);
    }
}
