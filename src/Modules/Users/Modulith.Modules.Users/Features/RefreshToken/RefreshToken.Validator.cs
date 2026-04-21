using FluentValidation;

namespace Modulith.Modules.Users.Features.RefreshToken;

internal sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
