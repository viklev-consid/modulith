namespace Modulith.Modules.Organizations.Contracts.Events;

public sealed record OrganizationMemberRoleChangedV1(
    Guid OrganizationId,
    Guid UserId,
    string OldRole,
    string NewRole,
    Guid ChangedByUserId,
    Guid EventId);
