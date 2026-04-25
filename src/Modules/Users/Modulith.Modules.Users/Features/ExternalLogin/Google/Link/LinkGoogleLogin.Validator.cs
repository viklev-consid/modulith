using FluentValidation;

namespace Modulith.Modules.Users.Features.ExternalLogin.Google.Link;

internal sealed class LinkGoogleLoginValidator : AbstractValidator<LinkGoogleLoginRequest>
{
    public LinkGoogleLoginValidator()
    {
        RuleFor(x => x.IdToken).NotEmpty();
    }
}
