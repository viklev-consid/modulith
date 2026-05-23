namespace Modulith.Modules.Organizations.Contracts.Events;

public sealed record OrganizationMemberAddedV1(
    Guid OrganizationId,
    Guid UserId,
    string Role,
    Guid EventId);
