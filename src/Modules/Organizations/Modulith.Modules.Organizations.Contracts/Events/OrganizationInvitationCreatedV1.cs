namespace Modulith.Modules.Organizations.Contracts.Events;

public sealed record OrganizationInvitationCreatedV1(
    Guid OrganizationId,
    Guid InvitationId,
    string Email,
    string Role,
    string RawToken,
    Guid InvitedByUserId,
    Guid EventId);
