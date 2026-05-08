namespace Modulith.Modules.Users.Contracts.Events;

public sealed record UserErasureRequestedV1(Guid UserId, string? DisplayName, Guid EventId);
