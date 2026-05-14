namespace Modulith.Modules.Users.Contracts.Events;

public sealed record RecoveryCodesRegeneratedV1(Guid UserId, string Email, Guid EventId);
