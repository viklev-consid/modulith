using FluentValidation;

namespace Modulith.Modules.Users.Features.ExternalLogin.Google.Login;

internal sealed class GoogleLoginValidator : AbstractValidator<GoogleLoginRequest>
{
    public GoogleLoginValidator()
    {
        RuleFor(x => x.IdToken).NotEmpty();
    }
}
