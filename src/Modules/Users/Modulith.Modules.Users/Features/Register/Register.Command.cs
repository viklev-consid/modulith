namespace Modulith.Modules.Users.Features.Register;

internal sealed record RegisterCommand(string Email, string Password, string DisplayName);
