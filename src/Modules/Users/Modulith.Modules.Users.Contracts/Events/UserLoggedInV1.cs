namespace Modulith.Modules.Users.Contracts.Events;

public sealed record UserLoggedInV1(Guid UserId, string Email, string IpAddress);
