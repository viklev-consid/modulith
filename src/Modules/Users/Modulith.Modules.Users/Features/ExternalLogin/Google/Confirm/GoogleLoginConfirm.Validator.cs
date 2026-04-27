using FluentValidation;

namespace Modulith.Modules.Users.Features.ExternalLogin.Google.Confirm;

internal sealed class GoogleLoginConfirmValidator : AbstractValidator<GoogleLoginConfirmRequest>
{
    public GoogleLoginConfirmValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(64);
    }
}
