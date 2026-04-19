namespace Modulith.Modules.Users.Contracts.Events;

public sealed record UserRegisteredV1(Guid UserId, string Email, string DisplayName);
