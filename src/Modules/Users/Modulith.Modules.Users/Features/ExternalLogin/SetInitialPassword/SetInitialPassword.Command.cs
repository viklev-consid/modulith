namespace Modulith.Modules.Users.Features.ExternalLogin.SetInitialPassword;

public sealed record SetInitialPasswordCommand(Guid UserId, string Password, string? ActiveRefreshTokenId);
