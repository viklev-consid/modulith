using FluentValidation;

namespace Modulith.Modules.Organizations.Features.CreateOrganization;

internal sealed class CreateOrganizationValidator : AbstractValidator<CreateOrganizationRequest>
{
    public CreateOrganizationValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Slug).MaximumLength(100);
    }
}
