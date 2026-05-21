using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Audit.Contracts.Queries;
using Modulith.Modules.Audit.Errors;
using Modulith.Modules.Audit.Persistence;
using Modulith.Shared.Kernel.Pagination;

namespace Modulith.Modules.Audit.Features.ListOrganizationAuditEntries;

public sealed class ListOrganizationAuditEntriesHandler(AuditDbContext db)
{
    public async Task<ErrorOr<ListOrganizationAuditEntriesResponse>> Handle(ListOrganizationAuditEntriesQuery query, CancellationToken ct)
        => await AuditTelemetry.InstrumentAsync(nameof(ListOrganizationAuditEntriesHandler), () => HandleCoreAsync(query, ct));

    private async Task<ErrorOr<ListOrganizationAuditEntriesResponse>> HandleCoreAsync(ListOrganizationAuditEntriesQuery query, CancellationToken ct)
    {
        if (query.Page <= 0 || query.Page > PageRequest.MaxPage)
        {
            return AuditErrors.PageInvalid;
        }

        if (query.PageSize <= 0 || query.PageSize > PageRequest.MaxPageSize)
        {
            return AuditErrors.PageSizeInvalid;
        }

        var pagination = PageRequest.Of(query.Page, query.PageSize);

        var baseQuery = db.AuditEntries
            .AsNoTracking()
            .Where(e => e.OrganizationId == query.OrganizationId)
            .OrderByDescending(e => e.OccurredAt);

        var total = await baseQuery.CountAsync(ct);

        var entries = await baseQuery
            .Skip(pagination.Offset)
            .Take(pagination.PageSize)
            .Select(e => new OrganizationAuditEntryDto(
                e.Id.Value,
                e.EventType,
                e.ActorId,
                e.ResourceType,
                e.ResourceId,
                e.OccurredAt,
                e.Payload))
            .ToListAsync(ct);

        return new ListOrganizationAuditEntriesResponse(entries, total, pagination.Page, pagination.PageSize);
    }
}
