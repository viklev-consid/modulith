namespace Modulith.Modules.Users.Features.ExternalLogin.Google.Login;

public sealed record GoogleLoginCommand(string IdToken, string? IpAddress, string? UserAgent);
