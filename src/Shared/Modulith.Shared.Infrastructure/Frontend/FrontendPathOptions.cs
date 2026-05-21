using System.ComponentModel.DataAnnotations;

namespace Modulith.Shared.Infrastructure.Frontend;

public sealed class FrontendPathOptions
{
    [Required]
    public string ConfirmEmail { get; init; } = "/confirm-email";

    [Required]
    public string ConfirmEmailChange { get; init; } = "/confirm-email-change";

    [Required]
    public string ResetPassword { get; init; } = "/reset-password";

    [Required]
    public string UserInvitation { get; init; } = "/register/invitation";

    [Required]
    public string OrganizationInvitation { get; init; } = "/register/organization-invitation";
}
