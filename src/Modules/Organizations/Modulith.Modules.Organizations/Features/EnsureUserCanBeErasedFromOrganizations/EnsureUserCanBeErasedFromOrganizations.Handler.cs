using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Organizations.Contracts.Commands;
using Modulith.Modules.Organizations.Domain;
using Modulith.Modules.Organizations.Persistence;

namespace Modulith.Modules.Organizations.Features.EnsureUserCanBeErasedFromOrganizations;

public sealed class EnsureUserCanBeErasedFromOrganizationsHandler(OrganizationsDbContext db)
{
    public async Task<ErrorOr<EnsureUserCanBeErasedFromOrganizationsResponse>> Handle(EnsureUserCanBeErasedFromOrganizationsCommand cmd, CancellationToken ct)
    {
        var ownedOrganizations = await db.Memberships
            .AsNoTracking()
            .Where(m => m.UserId == cmd.UserId && m.IsActive && m.Role == OrganizationRole.Owner)
            .Join(
                db.Organizations.AsNoTracking().Where(o => !o.IsDeleted),
                membership => membership.OrganizationId,
                organization => organization.Id,
                (membership, organization) => new
                {
                    organization.Id,
                    organization.Name,
                    organization.Slug,
                    membership.Role
                })
            .OrderBy(o => o.Name)
            .ToArrayAsync(ct);

        if (ownedOrganizations.Length == 0)
        {
            return new EnsureUserCanBeErasedFromOrganizationsResponse([]);
        }

        var organizationIds = ownedOrganizations.Select(o => o.Id).ToArray();
        var ownerCounts = await db.Memberships
            .AsNoTracking()
            .Where(m => organizationIds.Contains(m.OrganizationId) && m.IsActive && m.Role == OrganizationRole.Owner)
            .GroupBy(m => m.OrganizationId)
            .Select(g => new { OrganizationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OrganizationId, x => x.Count, ct);

        var blockers = ownedOrganizations
            .Select(o => new UserErasureBlockingOrganization(
                o.Id.Value,
                o.Name,
                o.Slug.Value,
                o.Role.Name,
                ownerCounts.TryGetValue(o.Id, out var count) && count == 1))
            .ToArray();

        return new EnsureUserCanBeErasedFromOrganizationsResponse(blockers);
    }
}
