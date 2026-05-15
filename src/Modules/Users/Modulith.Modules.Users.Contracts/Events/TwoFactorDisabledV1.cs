namespace Modulith.Modules.Users.Contracts.Events;

public sealed record TwoFactorDisabledV1(Guid UserId, string Email, string Method, Guid EventId);
