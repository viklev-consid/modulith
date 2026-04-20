using Modulith.Modules.Audit.Contracts.Dtos;

namespace Modulith.Modules.Audit.Contracts.Queries;

public sealed record GetAuditTrailQuery(Guid UserId, int Page = 1, int PageSize = 20);

public sealed record GetAuditTrailResponse(
    IReadOnlyList<AuditEntryDto> Entries,
    int Total,
    int Page,
    int PageSize);
