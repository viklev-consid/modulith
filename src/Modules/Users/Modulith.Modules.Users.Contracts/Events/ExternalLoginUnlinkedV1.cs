namespace Modulith.Modules.Users.Contracts.Events;

public sealed record ExternalLoginUnlinkedV1(
    Guid UserId,
    string Email,
    string Provider,
    Guid EventId);
