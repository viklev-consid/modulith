using FluentValidation;
using Microsoft.Extensions.Options;

namespace Modulith.Modules.Users.Features.Register;

internal sealed class RegisterValidator : AbstractValidator<RegisterRequest>
{
    public RegisterValidator(IOptions<UsersOptions> options)
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(254)
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(options.Value.MinPasswordLength)
            .MaximumLength(128);

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.InvitationToken)
            .MaximumLength(64);

        RuleFor(x => x.OrganizationInvitationToken)
            .MaximumLength(64);
    }
}
