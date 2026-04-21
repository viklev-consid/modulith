namespace Modulith.Modules.Users.Contracts.Events;

public sealed record PasswordResetV1(Guid UserId, string Email);
