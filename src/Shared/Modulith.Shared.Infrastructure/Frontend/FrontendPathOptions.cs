using System.ComponentModel.DataAnnotations;

namespace Modulith.Shared.Infrastructure.Frontend;

public sealed class FrontendPathOptions
{
    [Required]
    public string ConfirmEmail { get; init; } = "/confirm-email";

    [Required]
    public string GoogleConfirm { get; init; } = "/auth/google/confirm";

    [Required]
    public string ResetPassword { get; init; } = "/reset-password";
}

