using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Organizations.Authorization;
using Modulith.Modules.Organizations.Persistence;

namespace Modulith.Modules.Organizations.Features.ListMyOrganizations;

public sealed class ListMyOrganizationsHandler(OrganizationsDbContext db)
{
    public async Task<ErrorOr<ListMyOrganizationsResponse>> Handle(ListMyOrganizationsQuery query, CancellationToken ct)
    {
        var rows = await db.Memberships
            .AsNoTracking()
            .Where(m => m.UserId == query.UserId && m.IsActive)
            .Join(
                db.Organizations.AsNoTracking().Where(o => !o.IsDeleted),
                membership => membership.OrganizationId,
                organization => organization.Id,
                (membership, organization) => new
                {
                    OrganizationId = organization.Id,
                    organization.Name,
                    organization.Slug,
                    membership.Role,
                })
            .OrderBy(o => o.Name)
            .ToArrayAsync(ct);

        var organizations = rows
            .Select(row => new MyOrganizationItem(
                row.OrganizationId.Value,
                row.Name,
                row.Slug.Value,
                row.Role.Name,
                OrganizationRolePermissionMap.GetPermissions(row.Role),
                OrganizationRolePermissionMap.GetVersion(row.Role)))
            .ToArray();

        return new ListMyOrganizationsResponse(organizations);
    }
}
