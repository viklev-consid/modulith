using FluentValidation;
using Modulith.Modules.Organizations.Domain;

namespace Modulith.Modules.Organizations.Features.CreateOrganizationInvitation;

internal sealed class CreateOrganizationInvitationValidator : AbstractValidator<CreateOrganizationInvitationRequest>
{
    public CreateOrganizationInvitationValidator()
    {
        RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(r => r.Role)
            .NotEmpty()
            .MaximumLength(32)
            .Must(role => !OrganizationRole.Create(role).IsError)
            .WithMessage("Organization role is not valid.");
    }
}
