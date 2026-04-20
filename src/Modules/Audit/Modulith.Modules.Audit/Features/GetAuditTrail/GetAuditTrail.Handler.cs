using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Audit.Contracts.Dtos;
using Modulith.Modules.Audit.Contracts.Queries;
using Modulith.Modules.Audit.Persistence;

namespace Modulith.Modules.Audit.Features.GetAuditTrail;

public sealed class GetAuditTrailHandler(AuditDbContext db)
{
    public async Task<ErrorOr<GetAuditTrailResponse>> Handle(GetAuditTrailQuery query, CancellationToken ct)
    {
        var baseQuery = db.AuditEntries
            .AsNoTracking()
            .Where(e => e.ActorId == query.UserId)
            .OrderByDescending(e => e.OccurredAt);

        var total = await baseQuery.CountAsync(ct);

        var entries = await baseQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(e => new AuditEntryDto(
                e.Id.Value,
                e.EventType,
                e.ActorId,
                e.ResourceType,
                e.ResourceId,
                e.OccurredAt))
            .ToListAsync(ct);

        return new GetAuditTrailResponse(entries, total, query.Page, query.PageSize);
    }
}
