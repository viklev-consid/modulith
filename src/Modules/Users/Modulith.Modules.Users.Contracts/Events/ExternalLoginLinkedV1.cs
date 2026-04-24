namespace Modulith.Modules.Users.Contracts.Events;

public sealed record ExternalLoginLinkedV1(
    Guid UserId,
    string Provider,
    string Subject,
    DateTimeOffset LinkedAt,
    Guid EventId);
