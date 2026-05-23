namespace Modulith.Modules.Audit.Contracts.Queries;

public sealed record ListOrganizationAuditEntriesQuery(Guid OrganizationId, int Page = 1, int PageSize = 20);

public sealed record ListOrganizationAuditEntriesResponse(
    IReadOnlyList<OrganizationAuditEntryDto> Entries,
    int Total,
    int Page,
    int PageSize);

public sealed record OrganizationAuditEntryDto(
    Guid Id,
    string EventType,
    Guid? ActorId,
    string? ResourceType,
    Guid? ResourceId,
    DateTimeOffset OccurredAt,
    string Payload);
