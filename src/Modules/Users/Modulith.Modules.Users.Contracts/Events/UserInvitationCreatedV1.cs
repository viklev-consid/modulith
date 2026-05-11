namespace Modulith.Modules.Users.Contracts.Events;

public sealed record UserInvitationCreatedV1(
    Guid InvitationId,
    string Email,
    string Token,
    DateTimeOffset ExpiresAt,
    Guid MessageId);
