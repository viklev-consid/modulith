namespace Modulith.Modules.Users.Contracts.Events;

public sealed record ExternalLoginLinkedV1(
    Guid UserId,
    string Email,
    string Provider,
    string Subject,
    DateTimeOffset LinkedAt,
    Guid EventId);
