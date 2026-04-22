using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Audit.Authorization;
using Modulith.Modules.Audit.Contracts.Dtos;
using Modulith.Modules.Audit.Contracts.Queries;
using Modulith.Modules.Audit.Errors;
using Modulith.Modules.Audit.Persistence;
using Modulith.Shared.Infrastructure.Authorization;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Audit.Features.GetAuditTrail;

public sealed class GetAuditTrailHandler(
    AuditDbContext db,
    ICurrentUser currentUser,
    IResourcePolicy<AuditTrailResource> policy)
{
    public async Task<ErrorOr<GetAuditTrailResponse>> Handle(GetAuditTrailQuery query, CancellationToken ct)
        => await AuditTelemetry.InstrumentAsync(nameof(GetAuditTrailHandler), () => HandleCoreAsync(query, ct));

    private async Task<ErrorOr<GetAuditTrailResponse>> HandleCoreAsync(GetAuditTrailQuery query, CancellationToken ct)
    {
        var resource = new AuditTrailResource(query.UserId);
        if (!policy.IsAuthorized(currentUser, resource))
        {
            return AuditErrors.Forbidden;
        }

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
