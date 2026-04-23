namespace Modulith.Modules.Users.Contracts.Events;

public sealed record PasswordChangedV1(Guid UserId, string Email, Guid EventId);
