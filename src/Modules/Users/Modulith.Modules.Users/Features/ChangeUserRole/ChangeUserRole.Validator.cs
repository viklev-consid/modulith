using FluentValidation;

namespace Modulith.Modules.Users.Features.ChangeUserRole;

internal sealed class ChangeUserRoleValidator : AbstractValidator<ChangeUserRoleRequest>
{
    private static readonly System.Text.RegularExpressions.Regex ValidPattern =
        new(@"^[a-z][a-z0-9_-]{1,31}$",
            System.Text.RegularExpressions.RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(100));

    public ChangeUserRoleValidator()
    {
        RuleFor(r => r.Role)
            .NotEmpty()
            .WithMessage("Role is required.")
            .Matches(ValidPattern)
            .WithMessage("Role must match ^[a-z][a-z0-9_-]{1,31}$ (lowercase ASCII, no spaces).");
    }
}
