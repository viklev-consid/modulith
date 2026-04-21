namespace Modulith.Modules.Users.Features.ChangePassword;

public sealed record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword, string? ActiveRefreshTokenId);
