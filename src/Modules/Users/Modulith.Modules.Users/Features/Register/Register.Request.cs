namespace Modulith.Modules.Users.Features.Register;

public sealed record RegisterRequest(string Email, string Password, string DisplayName);
