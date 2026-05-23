using Modulith.Modules.Audit.Contracts.Queries;

namespace Modulith.Modules.Organizations.Features.GetOrganizationAudit;

public sealed record GetOrganizationAuditResponse(
    Guid OrganizationId,
    string AccessMode,
    IReadOnlyList<OrganizationAuditEntryDto> Entries,
    int Total,
    int Page,
    int PageSize);
