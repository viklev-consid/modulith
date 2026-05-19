namespace Modulith.Modules.Users.Contracts.Events;

public sealed record UserEmailConfirmedV1(Guid UserId, string Email, string DisplayName, Guid EventId);
