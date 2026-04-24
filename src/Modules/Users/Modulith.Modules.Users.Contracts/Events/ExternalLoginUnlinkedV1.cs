namespace Modulith.Modules.Users.Contracts.Events;

public sealed record ExternalLoginUnlinkedV1(
    Guid UserId,
    string Provider,
    Guid EventId);
