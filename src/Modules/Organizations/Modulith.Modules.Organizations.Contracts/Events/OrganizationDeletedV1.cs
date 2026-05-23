namespace Modulith.Modules.Organizations.Contracts.Events;

public sealed record OrganizationDeletedV1(
    Guid OrganizationId,
    Guid DeletedByUserId,
    Guid EventId);
