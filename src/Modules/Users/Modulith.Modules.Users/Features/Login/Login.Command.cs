namespace Modulith.Modules.Users.Features.Login;

public sealed record LoginCommand(string Email, string Password, string? IpAddress = null);
