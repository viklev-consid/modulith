namespace Modulith.Modules.Organizations.Contracts.Events;

public sealed record OrganizationCreatedV1(
    Guid OrganizationId,
    string Name,
    string Slug,
    Guid CreatedByUserId,
    Guid EventId);
