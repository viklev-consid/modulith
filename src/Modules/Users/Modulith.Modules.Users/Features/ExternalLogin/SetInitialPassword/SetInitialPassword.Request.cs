namespace Modulith.Modules.Users.Features.ExternalLogin.SetInitialPassword;

public sealed record SetInitialPasswordRequest(string Password, string GoogleIdToken);
