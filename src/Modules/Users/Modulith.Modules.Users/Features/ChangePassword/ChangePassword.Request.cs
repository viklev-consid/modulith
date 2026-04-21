namespace Modulith.Modules.Users.Features.ChangePassword;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
