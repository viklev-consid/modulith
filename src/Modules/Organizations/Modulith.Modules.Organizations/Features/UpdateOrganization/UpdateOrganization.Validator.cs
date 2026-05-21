using FluentValidation;

namespace Modulith.Modules.Organizations.Features.UpdateOrganization;

internal sealed class UpdateOrganizationValidator : AbstractValidator<UpdateOrganizationRequest>
{
    public UpdateOrganizationValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Slug).NotEmpty().MaximumLength(100);
    }
}
