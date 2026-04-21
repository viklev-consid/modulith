namespace Modulith.Modules.Users.Features.ResetPassword;

public sealed record ResetPasswordRequest(string Token, string NewPassword);
