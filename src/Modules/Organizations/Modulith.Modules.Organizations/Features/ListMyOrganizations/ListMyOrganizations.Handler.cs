using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Organizations.Authorization;
using Modulith.Modules.Organizations.Persistence;

namespace Modulith.Modules.Organizations.Features.ListMyOrganizations;

public sealed class ListMyOrganizationsHandler(OrganizationsDbContext db)
{
    public async Task<ErrorOr<ListMyOrganizationsResponse>> Handle(ListMyOrganizationsQuery query, CancellationToken ct)
    {
        var organizations = await db.Memberships
            .AsNoTracking()
            .Where(m => m.UserId == query.UserId && m.IsActive)
            .Join(
                db.Organizations.AsNoTracking().Where(o => !o.IsDeleted),
                membership => membership.OrganizationId,
                organization => organization.Id,
                (membership, organization) => new MyOrganizationItem(
                    organization.Id.Value,
                    organization.Name,
                    organization.Slug.Value,
                    membership.Role.Name,
                    OrganizationRolePermissionMap.GetPermissions(membership.Role),
                    OrganizationRolePermissionMap.GetVersion(membership.Role)))
            .OrderBy(o => o.Name)
            .ToArrayAsync(ct);

        return new ListMyOrganizationsResponse(organizations);
    }
}
