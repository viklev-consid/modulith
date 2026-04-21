using FluentValidation;

namespace Modulith.Modules.Users.Features.Logout;

internal sealed class LogoutValidator : AbstractValidator<LogoutRequest>
{
    public LogoutValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
