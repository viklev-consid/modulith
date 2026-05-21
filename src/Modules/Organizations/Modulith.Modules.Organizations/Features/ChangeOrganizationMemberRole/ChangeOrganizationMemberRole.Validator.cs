using FluentValidation;

namespace Modulith.Modules.Organizations.Features.ChangeOrganizationMemberRole;

internal sealed class ChangeOrganizationMemberRoleValidator : AbstractValidator<ChangeOrganizationMemberRoleRequest>
{
    public ChangeOrganizationMemberRoleValidator()
    {
        RuleFor(r => r.Role).NotEmpty().MaximumLength(32);
    }
}
