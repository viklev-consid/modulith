namespace Modulith.Modules.Organizations.Contracts.Events;

public sealed record OrganizationMemberRemovedV1(
    Guid OrganizationId,
    Guid UserId,
    Guid RemovedByUserId,
    Guid EventId);
