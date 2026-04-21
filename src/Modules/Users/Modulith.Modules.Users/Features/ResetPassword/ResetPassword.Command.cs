namespace Modulith.Modules.Users.Features.ResetPassword;

public sealed record ResetPasswordCommand(string Token, string NewPassword);
